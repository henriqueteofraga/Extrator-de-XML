-- 1. CRIA O BANCO ONDE FICA A CONFIG DAS QUERYS
CREATE DATABASE Extrator_Config;
GO

USE Extrator_Config;
GO

-- 2. CRIA A TABELA ONDE SALVA AS QUERYS
CREATE TABLE RegrasExtracao (
    Id VARCHAR(50) PRIMARY KEY,      
    Nome VARCHAR(100) NOT NULL,       
    QuerySql VARCHAR(MAX) NOT NULL,   
    Ativo BIT DEFAULT 1 NOT NULL,
    DataCriacao DATETIME DEFAULT GETDATE()
);
GO
-- 3. CRIA A REGRA DE TODAS AS VENDAS
USE Extrator_Config;
GO

INSERT INTO RegrasExtracao(Id,Nome,QuerySql)
VALUES(
'todas_as_vendas','Todas as Vendas','IF OBJECT_ID(''tempdb..#VendasElegiveis'') IS NOT NULL DROP TABLE #VendasElegiveis;
CREATE TABLE #VendasElegiveis (CodigoProduto BIGINT PRIMARY KEY);
INSERT INTO #VendasElegiveis (CodigoProduto)
select Codigo from Gestao.dbo.Vendas');


-- 4. CRIA A REGRA CESTA BASICA
USE Extrator_Config;
GO

INSERT INTO RegrasExtracao (Id, Nome, QuerySql)
VALUES (
    'vendas_cesta_basica', 
    'Vendas Cesta Basica', 
    'IF OBJECT_ID(''tempdb..#VendasElegiveis'') IS NOT NULL DROP TABLE #VendasElegiveis;
    CREATE TABLE #VendasElegiveis (CodigoProduto BIGINT PRIMARY KEY);

    INSERT INTO #VendasElegiveis (CodigoProduto)
    SELECT DISTINCT CAST(p.Codigo AS BIGINT)
    FROM Gestao.dbo.Produtos P
    INNER JOIN Gestao.dbo.Grupos G ON P.Grupo = G.Codigo
    LEFT JOIN Gestao.dbo.Grupos GPAI ON GPAI.Codigo = Gestao.dbo.FN_GRUPO_PAI(G.Codigo)
    LEFT JOIN Gestao.dbo.Grupos GPAIIMED ON GPAIIMED.Codigo = Gestao.dbo.FN_GRUPO_PAI_IMEDIATO(G.Codigo)
    WHERE 
    (
        (P.Aliquota = 1 AND ((G.Nome LIKE ''OVO%'' OR GPAI.Nome LIKE ''OVO%'' OR GPAIIMED.Nome LIKE ''OVO%'' OR P.Descricao LIKE ''OVO%'') OR ((G.Nome LIKE ''HORT%'' OR GPAI.Nome LIKE ''HORT%'' OR GPAIIMED.Nome LIKE ''HORT%'') AND NOT (G.Nome LIKE ''FLOR%'' OR GPAI.Nome LIKE ''FLOR%'' OR GPAIIMED.Nome LIKE ''FLOR%'' OR P.Descricao LIKE ''FLOR%'')) OR (G.Nome LIKE ''LEITE%'' OR GPAI.Nome LIKE ''LEITE%'' OR GPAIIMED.Nome LIKE ''LEITE%'' OR P.Descricao LIKE ''LEITE%'') OR (G.Nome LIKE ''FLOR%'' OR GPAI.Nome LIKE ''FLOR%'' OR GPAIIMED.Nome LIKE ''FLOR%'' OR P.Descricao LIKE ''FLOR%'')))
        OR (P.Aliquota = 7 AND P.AliquotaIcmsNFSaidas = 12 AND ((G.Nome LIKE ''PAO%'' OR GPAI.Nome LIKE ''PAO%'' OR GPAIIMED.Nome LIKE ''PAO%'' OR P.Descricao LIKE ''PAO%'') OR (G.Nome LIKE ''ARROZ%'' OR GPAI.Nome LIKE ''ARROZ%'' OR GPAIIMED.Nome LIKE ''ARROZ%'' OR P.Descricao LIKE ''ARROZ%'') OR ((G.Nome LIKE ''PEIXE%'' OR GPAI.Nome LIKE ''PEIXE%'' OR GPAIIMED.Nome LIKE ''PEIXE%'' OR P.Descricao LIKE ''PEIXE%'') AND P.BASE_REDUZIDA_ICMS > 0) OR ((G.Nome LIKE ''ERVA%'' OR GPAI.Nome LIKE ''ERVA%'' OR GPAIIMED.Nome LIKE ''ERVA%'' OR P.Descricao LIKE ''ERVA%'' OR P.Descricao LIKE ''%MATE%'') AND P.BASE_REDUZIDA_ICMS > 0) OR ((G.Nome LIKE ''FARINHA%'' OR GPAI.Nome LIKE ''FARINHA%'' OR GPAIIMED.Nome LIKE ''FARINHA%'' OR P.Descricao LIKE ''FARINHA%'') AND P.BASE_REDUZIDA_ICMS > 0) OR ((G.Nome LIKE ''MASSA%'' OR GPAI.Nome LIKE ''MASSA%'' OR GPAIIMED.Nome LIKE ''MASSA%'' OR P.Descricao LIKE ''MASSA%'') AND P.BASE_REDUZIDA_ICMS > 0) OR ((G.Nome LIKE ''FEIJAO%'' OR GPAI.Nome LIKE ''FEIJAO%'' OR GPAIIMED.Nome LIKE ''FEIJAO%'' OR P.Descricao LIKE ''FEIJAO%'') AND P.BASE_REDUZIDA_ICMS > 0) OR ((G.Nome LIKE ''ALHO%'' OR GPAI.Nome LIKE ''ALHO%'' OR GPAIIMED.Nome LIKE ''ALHO%'' OR P.Descricao LIKE ''ALHO%'') AND P.BASE_REDUZIDA_ICMS > 0)))
        OR (P.Aliquota = 3 AND P.BaseReduzidaST > 0 AND (G.Nome LIKE ''CARNE%'' OR GPAI.Nome LIKE ''CARNE%'' OR GPAIIMED.Nome LIKE ''CARNE%'' OR G.Nome LIKE ''A�OUGUE%'' OR GPAI.Nome LIKE ''A�OUGUE%'' OR GPAIIMED.Nome LIKE ''A�OUGUE%''))
        OR (P.BASE_REDUZIDA_ICMS > 0 AND P.Aliquota = 7 AND P.AliquotaIcmsNFSaidas IN (12, 17) AND NOT (G.Nome LIKE ''PAO%'' OR GPAI.Nome LIKE ''PAO%'' OR GPAIIMED.Nome LIKE ''PAO%'' OR P.Descricao LIKE ''PAO%'') AND NOT (G.Nome LIKE ''ARROZ%'' OR GPAI.Nome LIKE ''ARROZ%'' OR GPAIIMED.Nome LIKE ''ARROZ%'' OR P.Descricao LIKE ''ARROZ%'') AND NOT (G.Nome LIKE ''PEIXE%'' OR GPAI.Nome LIKE ''PEIXE%'' OR GPAIIMED.Nome LIKE ''PEIXE%'' OR P.Descricao LIKE ''PEIXE%'') AND NOT (G.Nome LIKE ''ERVA%'' OR GPAI.Nome LIKE ''ERVA%'' OR GPAIIMED.Nome LIKE ''ERVA%'' OR P.Descricao LIKE ''ERVA%'' OR P.Descricao LIKE ''%MATE%'') AND NOT (G.Nome LIKE ''FARINHA%'' OR GPAI.Nome LIKE ''FARINHA%'' OR GPAIIMED.Nome LIKE ''FARINHA%'' OR P.Descricao LIKE ''FARINHA%'') AND NOT (G.Nome LIKE ''MASSA%'' OR GPAI.Nome LIKE ''MASSA%'' OR GPAIIMED.Nome LIKE ''MASSA%'' OR P.Descricao LIKE ''MASSA%'') AND NOT (G.Nome LIKE ''FEIJAO%'' OR GPAI.Nome LIKE ''FEIJAO%'' OR GPAIIMED.Nome LIKE ''FEIJAO%'' OR P.Descricao LIKE ''FEIJAO%'') AND NOT (G.Nome LIKE ''ALHO%'' OR GPAI.Nome LIKE ''ALHO%'' OR GPAIIMED.Nome LIKE ''ALHO%'' OR P.Descricao LIKE ''ALHO%''))
        OR ((G.Nome LIKE ''FARINHA%'' OR GPAI.Nome LIKE ''FARINHA%'' OR GPAIIMED.Nome LIKE ''FARINHA%'') AND (P.Descricao LIKE ''FAR%FOSF%'' OR P.Descricao LIKE ''FAR%ANTIOX%'' OR P.Descricao LIKE ''FAR%EMULS%'' OR P.Descricao LIKE ''FAR%VITAM%'' OR P.Descricao LIKE ''FAR%FERM%'' OR P.Descricao LIKE ''FAR%ARROZ%'' OR P.Descricao LIKE ''FAR%MAND%'' OR P.Descricao LIKE ''FAR%MILHO%'') AND P.AliquotaIcmsNFSaidas = 12 AND P.Aliquota IN (7, 12))
    );'
);
GO