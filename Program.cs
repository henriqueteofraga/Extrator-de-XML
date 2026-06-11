using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

// CONFIGURAÇÃO CENTRAL: String de conexão fixa para onde está o banco Extrator_Config (Mude o IP/Senha se necessário)
const string ConnectionStringConfigCentral = "Server=localhost;Database=Extrator_Config;User Id=sa;Password=a2m8x7h5;TrustServerCertificate=True;";

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

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("PermitirTudo");

app.MapHub<ExtracaoHubOtimizado>("/extracaoHub");

// ENDPOINT 1: Listar bancos do IP alvo digitado na tela
app.MapPost("/api/listar-bancos", async (ConexaoSqlRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Server) || string.IsNullOrWhiteSpace(request.Usuario) || string.IsNullOrWhiteSpace(request.Senha))
    {
        return Results.BadRequest("Todos os campos de conexão são obrigatórios.");
    }

    var connectionStringMaster = $"Server={request.Server};Database=master;User ID={request.Usuario};Password={request.Senha};TrustServerCertificate=True;Timeout=10";
    var bancos = new List<string>();

    try
    {
        using (var connection = new SqlConnection(connectionStringMaster))
        {
            await connection.OpenAsync();
            var query = "SELECT name FROM sys.databases WHERE name LIKE 'GestaoXML%' AND state = 0 ORDER BY name;";
            
            using (var command = new SqlCommand(query, connection))
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        bancos.Add(reader.GetString(0));
                    }
                }
            }
        }
        return Results.Ok(bancos);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Erro ao conectar ao SQL Server alvo: {ex.Message}");
    }
});

// ENDPOINT 2: Navegador de pastas do servidor da API
app.MapGet("/api/navegar-pastas", ([FromQuery] string? caminho) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(caminho))
        {
            var unidades = DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => d.Name).ToList();
            return Results.Ok(new { tipo = "unidades", itens = unidades });
        }

        if (!Directory.Exists(caminho)) return Results.BadRequest("O diretório não existe.");

        var subPastas = Directory.GetDirectories(caminho).Select(p => Path.GetFullPath(p)).OrderBy(p => p).ToList();
        return Results.Ok(new { tipo = "pastas", atual = Path.GetFullPath(caminho), itens = subPastas });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// ENDPOINT 3: Busca as queries do banco central administrativo (Independente do IP da tela)
app.MapGet("/api/listar-queries", async () =>
{
    var queries = new List<object>();
    try
    {
        using (var connection = new SqlConnection(ConnectionStringConfigCentral))
        {
            await connection.OpenAsync();
            var query = "SELECT Id, Nome FROM RegrasExtracao WHERE Ativo = 1 ORDER BY Nome;";
            
            using (var command = new SqlCommand(query, connection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    queries.Add(new { id = reader.GetString(0), nome = reader.GetString(1) });
                }
            }
        }
        return Results.Ok(queries);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Erro ao buscar regras no banco central: {ex.Message}");
    }
});

// ENDPOINT NOVO: Salvar uma nova regra de negócio dentro da tabela administrativa central
app.MapPost("/api/salvar-regra", async ([FromBody] CadastrarRegraRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Id) || 
        string.IsNullOrWhiteSpace(request.Nome) || 
        string.IsNullOrWhiteSpace(request.QuerySql))
    {
        return Results.BadRequest("Todos os campos do formulário são obrigatórios.");
    }

    try
    {
        using (var connection = new SqlConnection(ConnectionStringConfigCentral))
        {
            await connection.OpenAsync();
            
            // Query parametrizada clássica para evitar qualquer risco de SQL Injection ou quebra de aspas simples
            var query = "INSERT INTO RegrasExtracao (Id, Nome, QuerySql, Ativo) VALUES (@id, @nome, @querySql, 1);";
            
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@id", request.Id.Trim().ToLower());
                command.Parameters.AddWithValue("@nome", request.Nome.Trim());
                command.Parameters.AddWithValue("@querySql", request.QuerySql.Trim());
                
                await command.ExecuteNonQueryAsync();
            }
        }
        return Results.Ok(new { mensagem = "Regra armazenada com sucesso!" });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Erro ao gravar regra no banco de dados central: {ex.Message}");
    }
});

// ENDPOINT: Buscar todas as regras com o código SQL completo (Para o Visualizador/Editor)
app.MapGet("/api/regras-completo", async () =>
{
    var regras = new List<object>();
    try
    {
        using (var connection = new SqlConnection(ConnectionStringConfigCentral))
        {
            await connection.OpenAsync();
            var query = "SELECT Id, Nome, QuerySql FROM RegrasExtracao ORDER BY Nome;";
            
            using (var command = new SqlCommand(query, connection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    regras.Add(new { 
                        id = reader.GetString(0), 
                        nome = reader.GetString(1), 
                        querySql = reader.GetString(2) 
                    });
                }
            }
        }
        return Results.Ok(regras);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Erro ao buscar regras completas: {ex.Message}");
    }
});

// ENDPOINT: Atualizar uma regra existente (Editor)
app.MapPut("/api/atualizar-regra", async ([FromBody] AtualizarRegraRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Nome) || string.IsNullOrWhiteSpace(request.QuerySql))
    {
        return Results.BadRequest("Todos os campos são obrigatórios para atualização.");
    }

    try
    {
        using (var connection = new SqlConnection(ConnectionStringConfigCentral))
        {
            await connection.OpenAsync();
            var query = "UPDATE RegrasExtracao SET Nome = @nome, QuerySql = @querySql WHERE Id = @id;";
            
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@id", request.Id);
                command.Parameters.AddWithValue("@nome", request.Nome.Trim());
                command.Parameters.AddWithValue("@querySql", request.QuerySql.Trim());
                
                int linhasAfetadas = await command.ExecuteNonQueryAsync();
                if (linhasAfetadas == 0) return Results.NotFound("Regra não encontrada para atualização.");
            }
        }
        return Results.Ok(new { mensagem = "Regra atualizada com sucesso!" });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Erro ao atualizar regra no banco: {ex.Message}");
    }
});

// ENDPOINT: Excluir uma regra permanentemente (Exclusor)
app.MapDelete("/api/excluir-regra/{id}", async (string id) =>
{
    if (string.IsNullOrWhiteSpace(id)) return Results.BadRequest("ID inválido.");

    try
    {
        using (var connection = new SqlConnection(ConnectionStringConfigCentral))
        {
            await connection.OpenAsync();
            var query = "DELETE FROM RegrasExtracao WHERE Id = @id;";
            
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@id", id);
                int linhasAfetadas = await command.ExecuteNonQueryAsync();
                if (linhasAfetadas == 0) return Results.NotFound("Regra não encontrada para exclusão.");
            }
        }
        return Results.Ok(new { mensagem = "Regra excluída com sucesso permanentemente!" });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Erro ao excluir regra no banco: {ex.Message}");
    }
});
// ENDPOINT 4: Extração Distribuída Otimizada
app.MapPost("/api/extrair", async ([FromBody] ExtrairRequestOtimizado request, IHubContext<ExtracaoHubOtimizado> hubContext) =>
{
    // String de conexão para a máquina alvo onde estão os dados da extração
    string connectionStringDados = $"Server={request.Server};Database={request.Banco};User Id={request.Usuario};Password={request.Senha};TrustServerCertificate=True;";
    string pastaBase = request.CaminhoDestino;
    string logFile = Path.Combine(pastaBase, "erro_extracao.log");

    string querySelecionada = "";

    // PASSO A: Busca a regra fiscal usando a conexão do banco central de configurações
    try
    {
        using (var configConn = new SqlConnection(ConnectionStringConfigCentral))
        {
            await configConn.OpenAsync();
            var sqlBusca = "SELECT QuerySql FROM RegrasExtracao WHERE Id = @id AND Ativo = 1;";
            using (var configCmd = new SqlCommand(sqlBusca, configConn))
            {
                configCmd.Parameters.AddWithValue("@id", request.QueryId);
                var resultado = await configCmd.ExecuteScalarAsync();
                if (resultado == null) return Results.Problem("A regra selecionada não foi encontrada no banco central.");
                querySelecionada = resultado.ToString()!;
            }
        }
    }
    catch (Exception exConfig)
    {
        return Results.Problem($"Falha ao ler banco de configurações central: {exConfig.Message}");
    }

    // Bloco comum de mapeamento final de arquivos
    string sqlFinalComum = @"
        SELECT CAST(vxml.CodVenda AS BIGINT)
        FROM VendasXML vxml
        INNER JOIN Gestao.dbo.Vendas v ON vxml.CodVenda = v.Codigo
        WHERE v.Data_Hora BETWEEN @inicio AND @fim
          AND EXISTS (
              SELECT 1 
              FROM Gestao.dbo.VendasProdutos VP
              INNER JOIN #VendasElegiveis PE ON CAST(vp.CodigoProduto AS BIGINT) = PE.CodigoProduto
              WHERE vp.CodVenda = v.Codigo
          );

        IF OBJECT_ID('tempdb..#VendasElegiveis') IS NOT NULL DROP TABLE #VendasElegiveis;";

    string queryExecucaoCompleta = querySelecionada + sqlFinalComum;

    // PASSO B: Executa a consulta unificada no banco de dados alvo (cliente)
    try
    {
        if (!Directory.Exists(pastaBase)) Directory.CreateDirectory(pastaBase);

        int batchSize = 500;
        var codigos = new List<long>();

        using (SqlConnection conn = new SqlConnection(connectionStringDados))
        {
            await conn.OpenAsync();

            using (SqlCommand cmd = new SqlCommand(queryExecucaoCompleta, conn))
            {
                DateTime dataInicioConvertida = DateTime.Parse(request.DataInicio);
                DateTime dataFimConvertida = DateTime.Parse(request.DataFim);

                cmd.Parameters.AddWithValue("@inicio", dataInicioConvertida);
                cmd.Parameters.AddWithValue("@fim", dataFimConvertida.Date.AddHours(23).AddMinutes(59).AddSeconds(59));
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
        return Results.Ok(new { mensagem = "Extração concluída com sucesso!", total = codigos.Count });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, title: "Erro na extração");
    }
});

//app.MapGet("/", () => "API de Extração XML Distribuída rodando!");
app.Run();

public class ExtracaoHubOtimizado : Hub { }
public record ConexaoSqlRequest(string Server, string Usuario, string Senha);
public record ExtrairRequestOtimizado(string Server, string Usuario, string Senha, string Banco, string DataInicio, string DataFim, string CaminhoDestino, string QueryId);
public record CadastrarRegraRequest(string Id, string Nome, string QuerySql);
public record AtualizarRegraRequest(string Id, string Nome, string QuerySql);