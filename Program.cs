using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuração de CORS para SignalR
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirTudo", policy =>
    {
        policy.SetIsOriginAllowed(origin => true) 
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); 
    });
});

builder.Services.AddSignalR();

var app = builder.Build();

// 2. Middlewares
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("PermitirTudo");

// 3. Mapeamento de Endpoints
app.MapHub<ExtracaoHubOtimizado>("/extracaoHub");

app.MapPost("/api/extrair", async ([FromBody] ExtrairRequestOtimizado request, IHubContext<ExtracaoHubOtimizado> hubContext) =>
{
    string connectionString = "Server=10.10.40.55;Database=GestaoXML_13986009000165;User Id=sa;Password=a2m8x7h5;TrustServerCertificate=True;";
    string pastaBase = request.CaminhoDestino; 
    string logFile = Path.Combine(Directory.GetCurrentDirectory(), "erro_extracao.log");

    try
    {
        if (!Directory.Exists(pastaBase)) Directory.CreateDirectory(pastaBase);

        int batchSize = 500;
        var codigos = new List<long>();

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();

            string queryIds = @"
                IF OBJECT_ID('tempdb..#ProdutosElegiveis') IS NOT NULL DROP TABLE #ProdutosElegiveis;

                CREATE TABLE #ProdutosElegiveis (
                    CodigoProduto BIGINT PRIMARY KEY
                );

                INSERT INTO #ProdutosElegiveis (CodigoProduto)
                SELECT DISTINCT CAST(p.Codigo AS BIGINT)
                FROM Gestao.dbo.Produtos P
                INNER JOIN Gestao.dbo.Grupos G ON P.Grupo = G.Codigo
                LEFT JOIN Gestao.dbo.Grupos GPAI ON GPAI.Codigo = Gestao.dbo.FN_GRUPO_PAI(G.Codigo)
                LEFT JOIN Gestao.dbo.Grupos GPAIIMED ON GPAIIMED.Codigo = Gestao.dbo.FN_GRUPO_PAI_IMEDIATO(G.Codigo)
                WHERE 
                (
                    (
                        P.Aliquota = 1 AND (
                            (G.Nome LIKE 'OVO%' OR GPAI.Nome LIKE 'OVO%' OR GPAIIMED.Nome LIKE 'OVO%' OR P.Descricao LIKE 'OVO%')
                            OR (
                                (G.Nome LIKE 'HORT%' OR GPAI.Nome LIKE 'HORT%' OR GPAIIMED.Nome LIKE 'HORT%')
                                AND NOT (G.Nome LIKE 'FLOR%' OR GPAI.Nome LIKE 'FLOR%' OR GPAIIMED.Nome LIKE 'FLOR%' OR P.Descricao LIKE 'FLOR%')
                            )
                            OR (G.Nome LIKE 'LEITE%' OR GPAI.Nome LIKE 'LEITE%' OR GPAIIMED.Nome LIKE 'LEITE%' OR P.Descricao LIKE 'LEITE%')
                            OR (G.Nome LIKE 'FLOR%' OR GPAI.Nome LIKE 'FLOR%' OR GPAIIMED.Nome LIKE 'FLOR%' OR P.Descricao LIKE 'FLOR%')
                        )
                    )
                    OR 
                    (
                        P.Aliquota = 7 AND P.AliquotaIcmsNFSaidas = 12 AND (
                            (G.Nome LIKE 'PAO%' OR GPAI.Nome LIKE 'PAO%' OR GPAIIMED.Nome LIKE 'PAO%' OR P.Descricao LIKE 'PAO%')
                            OR (G.Nome LIKE 'ARROZ%' OR GPAI.Nome LIKE 'ARROZ%' OR GPAIIMED.Nome LIKE 'ARROZ%' OR P.Descricao LIKE 'ARROZ%')
                            OR ((G.Nome LIKE 'PEIXE%' OR GPAI.Nome LIKE 'PEIXE%' OR GPAIIMED.Nome LIKE 'PEIXE%' OR P.Descricao LIKE 'PEIXE%') AND P.BASE_REDUZIDA_ICMS > 0)
                            OR ((G.Nome LIKE 'ERVA%' OR GPAI.Nome LIKE 'ERVA%' OR GPAIIMED.Nome LIKE 'ERVA%' OR P.Descricao LIKE 'ERVA%' OR P.Descricao LIKE '%MATE%') AND P.BASE_REDUZIDA_ICMS > 0)
                            OR ((G.Nome LIKE 'FARINHA%' OR GPAI.Nome LIKE 'FARINHA%' OR GPAIIMED.Nome LIKE 'FARINHA%' OR P.Descricao LIKE 'FARINHA%') AND P.BASE_REDUZIDA_ICMS > 0)
                            OR ((G.Nome LIKE 'MASSA%' OR GPAI.Nome LIKE 'MASSA%' OR GPAIIMED.Nome LIKE 'MASSA%' OR P.Descricao LIKE 'MASSA%') AND P.BASE_REDUZIDA_ICMS > 0)
                            OR ((G.Nome LIKE 'FEIJAO%' OR GPAI.Nome LIKE 'FEIJAO%' OR GPAIIMED.Nome LIKE 'FEIJAO%' OR P.Descricao LIKE 'FEIJAO%') AND P.BASE_REDUZIDA_ICMS > 0)
                            OR ((G.Nome LIKE 'ALHO%' OR GPAI.Nome LIKE 'ALHO%' OR GPAIIMED.Nome LIKE 'ALHO%' OR P.Descricao LIKE 'ALHO%') AND P.BASE_REDUZIDA_ICMS > 0)
                        )
                    )
                    OR 
                    (
                        P.Aliquota = 3 AND P.BaseReduzidaST > 0
                        AND (G.Nome LIKE 'CARNE%' OR GPAI.Nome LIKE 'CARNE%' OR GPAIIMED.Nome LIKE 'CARNE%' OR G.Nome LIKE 'AÇOUGUE%' OR GPAI.Nome LIKE 'AÇOUGUE%' OR GPAIIMED.Nome LIKE 'AÇOUGUE%')
                    )
                    OR 
                    (
                        P.BASE_REDUZIDA_ICMS > 0 AND P.Aliquota = 7 AND P.AliquotaIcmsNFSaidas IN (12, 17)
                        AND NOT (G.Nome LIKE 'PAO%' OR GPAI.Nome LIKE 'PAO%' OR GPAIIMED.Nome LIKE 'PAO%' OR P.Descricao LIKE 'PAO%')
                        AND NOT (G.Nome LIKE 'ARROZ%' OR GPAI.Nome LIKE 'ARROZ%' OR GPAIIMED.Nome LIKE 'ARROZ%' OR P.Descricao LIKE 'ARROZ%')
                        AND NOT (G.Nome LIKE 'PEIXE%' OR GPAI.Nome LIKE 'PEIXE%' OR GPAIIMED.Nome LIKE 'PEIXE%' OR P.Descricao LIKE 'PEIXE%')
                        AND NOT (G.Nome LIKE 'ERVA%' OR GPAI.Nome LIKE 'ERVA%' OR GPAIIMED.Nome LIKE 'ERVA%' OR P.Descricao LIKE 'ERVA%' OR P.Descricao LIKE '%MATE%')    
                        AND NOT (G.Nome LIKE 'FARINHA%' OR GPAI.Nome LIKE 'FARINHA%' OR GPAIIMED.Nome LIKE 'FARINHA%' OR P.Descricao LIKE 'FARINHA%')
                        AND NOT (G.Nome LIKE 'MASSA%' OR GPAI.Nome LIKE 'MASSA%' OR GPAIIMED.Nome LIKE 'MASSA%' OR P.Descricao LIKE 'MASSA%')
                        AND NOT (G.Nome LIKE 'FEIJAO%' OR GPAI.Nome LIKE 'FEIJAO%' OR GPAIIMED.Nome LIKE 'FEIJAO%' OR P.Descricao LIKE 'FEIJAO%')
                        AND NOT (G.Nome LIKE 'ALHO%' OR GPAI.Nome LIKE 'ALHO%' OR GPAIIMED.Nome LIKE 'ALHO%' OR P.Descricao LIKE 'ALHO%')
                    )
                    OR      
                    (
                        (G.Nome LIKE 'FARINHA%' OR GPAI.Nome LIKE 'FARINHA%' OR GPAIIMED.Nome LIKE 'FARINHA%')
                        AND (P.Descricao LIKE 'FAR%FOSF%' OR P.Descricao LIKE 'FAR%ANTIOX%' OR P.Descricao LIKE 'FAR%EMULS%' OR P.Descricao LIKE 'FAR%VITAM%' OR P.Descricao LIKE 'FAR%FERM%' OR P.Descricao LIKE 'FAR%ARROZ%' OR P.Descricao LIKE 'FAR%MAND%' OR P.Descricao LIKE 'FAR%MILHO%')
                        AND P.AliquotaIcmsNFSaidas = 12 AND P.Aliquota IN (7, 12)
                    )
                );

                SELECT CAST(vxml.CodVenda AS BIGINT)
                FROM VendasXML vxml
                INNER JOIN Gestao.dbo.Vendas v ON vxml.CodVenda = v.Codigo
                WHERE v.Data_Hora BETWEEN @inicio AND @fim
                  AND EXISTS (
                      SELECT 1 
                      FROM Gestao.dbo.VendasProdutos VP
                      INNER JOIN #ProdutosElegiveis PE ON CAST(vp.CodigoProduto AS BIGINT) = PE.CodigoProduto
                      WHERE vp.CodVenda = v.Codigo
                  );

                IF OBJECT_ID('tempdb..#ProdutosElegiveis') IS NOT NULL DROP TABLE #ProdutosElegiveis;";

            using (SqlCommand cmd = new SqlCommand(queryIds, conn))
            {
                cmd.Parameters.AddWithValue("@inicio", request.DataInicio);
                cmd.Parameters.AddWithValue("@fim", request.DataFim.Date.AddHours(23).AddMinutes(59).AddSeconds(59));
                cmd.CommandTimeout = 0;

                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        if (!reader.IsDBNull(0)) codigos.Add(reader.GetInt64(0));
                    }
                }
            }

            int totalVendas = codigos.Count;
            await hubContext.Clients.All.SendAsync("ReceberTotal", totalVendas);

            if (totalVendas == 0)
                return Results.Ok(new { mensagem = "Nenhuma venda encontrada.", total = 0 });

            int totalLotes = (int)Math.Ceiling((double)totalVendas / batchSize);

            for (int i = 0; i < totalLotes; i++)
            {
                try
                {
                    var lote = codigos.Skip(i * batchSize).Take(batchSize).ToList();
                    string listaIds = string.Join(",", lote);
                    string queryXml = $"SELECT NomeArquivo, ArquivoXML FROM VendasXML WHERE CodVenda IN ({listaIds})";

                    using (SqlCommand cmd = new SqlCommand(queryXml, conn))
                    {
                        cmd.CommandTimeout = 0;
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                if (reader.IsDBNull(1)) continue;

                                string nomeOriginal = reader.IsDBNull(0) ? $"arq_{Guid.NewGuid()}.zip" : reader.GetString(0);
                                byte[] arquivoBytes = (byte[])reader.GetValue(1);

                                string nomeZip = nomeOriginal.Replace(".rar", ".zip", StringComparison.OrdinalIgnoreCase);
                                string caminhoZip = Path.Combine(pastaBase, nomeZip);
                                
                                await File.WriteAllBytesAsync(caminhoZip, arquivoBytes);

                                try 
                                {
                                    ZipFile.ExtractToDirectory(caminhoZip, pastaBase, overwriteFiles: true);
                                    File.Delete(caminhoZip); 
                                }
                                catch (Exception exZip)
                                {
                                    await File.AppendAllTextAsync(logFile, $"[{DateTime.Now}] Erro ao descompactar {nomeZip}: {exZip.Message}{Environment.NewLine}");
                                }
                            }
                        }
                    }

                    int progresso = (int)((double)(i + 1) / totalLotes * 100);
                    await hubContext.Clients.All.SendAsync("ReceberProgresso", progresso);
                }
                catch (Exception ex)
                {
                    string erroLote = $"[{DateTime.Now}] Erro no Lote {i}: {ex.Message}{Environment.NewLine}";
                    Console.WriteLine(erroLote);
                    await File.AppendAllTextAsync(logFile, erroLote); 
                }
            }
        }
        return Results.Ok(new { mensagem = "Extração concluída diretamente na raiz!", total = codigos.Count });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, title: "Erro na extração");
    }
});

app.MapGet("/", () => "API de Extração XML está rodando!");
app.Run("http://localhost:5157");

// Classes renomeadas para blindar contra duplicidade no projeto
public class ExtracaoHubOtimizado : Hub { }
public record ExtrairRequestOtimizado(DateTime DataInicio, DateTime DataFim, string CaminhoDestino);