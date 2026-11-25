/* Total de respostas por meio usado */
SELECT CASE choice.[value]
           WHEN 'iliturgia' THEN 'iLiturgia'
           WHEN 'outro-app' THEN 'Outro aplicativo'
           WHEN 'site' THEN 'Site ou p�gina'
           WHEN 'livro' THEN 'Livro impresso'
           WHEN 'youtube' THEN 'V�deos ou transmiss�es'
           WHEN 'nao-uso' THEN 'N�o usa atualmente'
           ELSE choice.[value]
       END AS Platform,
       COUNT(*) AS ResponseCount
FROM dbo.MelvLiturgiaFeedback AS response
CROSS APPLY OPENJSON(response.PlatformsJson) AS choice
GROUP BY choice.[value]
ORDER BY ResponseCount DESC;
GO

/* Frequ�ncia de uso */
SELECT Frequency, COUNT(*) AS ResponseCount
FROM dbo.MelvLiturgiaFeedback
GROUP BY Frequency
ORDER BY ResponseCount DESC;
GO

/* Dificuldades mais citadas */
SELECT CASE choice.[value]
           WHEN 'achar-conteudo' THEN 'Encontrar o conte�do do dia'
           WHEN 'entender-ordem' THEN 'Entender a ordem da Hora'
           WHEN 'calendario' THEN 'Calend�rio ou mem�ria lit�rgica'
           WHEN 'navegacao' THEN 'Navega��o entre partes'
           WHEN 'leitura-celular' THEN 'Leitura no celular'
           WHEN 'offline' THEN 'Uso offline ou �udio'
           WHEN 'conteudo-incompleto' THEN 'Conte�do incompleto ou desatualizado'
           WHEN 'outra-dificuldade' THEN 'Outra dificuldade'
           WHEN 'sem-dificuldades' THEN 'Sem dificuldades importantes'
           ELSE choice.[value]
       END AS Difficulty,
       COUNT(*) AS ResponseCount
FROM dbo.MelvLiturgiaFeedback AS response
CROSS APPLY OPENJSON(response.DifficultiesJson) AS choice
GROUP BY choice.[value]
ORDER BY ResponseCount DESC;
GO

/* Respostas abertas, em ordem da mais recente */
SELECT TOP (100) SubmittedAtUtc, Example, WorksWell, Suggestion
FROM dbo.MelvLiturgiaFeedback
WHERE Example IS NOT NULL OR WorksWell IS NOT NULL OR Suggestion IS NOT NULL
ORDER BY SubmittedAtUtc DESC;
GO

