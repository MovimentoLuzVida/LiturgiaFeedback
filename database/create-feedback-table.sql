/* Execute no banco SQL Server escolhido para a pesquisa. N�o cria nem altera outros bancos. */
IF OBJECT_ID(N'dbo.MelvLiturgiaFeedback', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MelvLiturgiaFeedback
    (
        ResponseId UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_MelvLiturgiaFeedback PRIMARY KEY,
        SubmittedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_MelvLiturgiaFeedback_SubmittedAtUtc DEFAULT SYSUTCDATETIME(),
        PlatformsJson NVARCHAR(MAX) NOT NULL,
        OtherPlatform NVARCHAR(100) NULL,
        ContentTypesJson NVARCHAR(MAX) NOT NULL,
        Frequency NVARCHAR(40) NOT NULL,
        DifficultiesJson NVARCHAR(MAX) NOT NULL,
        Example NVARCHAR(1500) NULL,
        WorksWell NVARCHAR(1000) NULL,
        Suggestion NVARCHAR(1000) NULL,
        CONSTRAINT CK_MelvLiturgiaFeedback_PlatformsJson CHECK (ISJSON(PlatformsJson) = 1),
        CONSTRAINT CK_MelvLiturgiaFeedback_ContentTypesJson CHECK (ISJSON(ContentTypesJson) = 1),
        CONSTRAINT CK_MelvLiturgiaFeedback_DifficultiesJson CHECK (ISJSON(DifficultiesJson) = 1)
    );

    CREATE INDEX IX_MelvLiturgiaFeedback_SubmittedAtUtc
        ON dbo.MelvLiturgiaFeedback (SubmittedAtUtc DESC);
END;
GO

IF OBJECT_ID(N'dbo.UserConference', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserConference
    (
        [Password] NVARCHAR(4000) NOT NULL
    );
END;
GO

-- Cadastre a senha manualmente no SQL Server Management Studio:
-- INSERT INTO dbo.UserConference ([Password]) VALUES (N'SUA-SENHA-LONGA-AQUI');

