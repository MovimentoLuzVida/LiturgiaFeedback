# Pesquisa MELV Liturgia - escuta da comunidade

Projeto dividido em duas partes:

- `web/`: formul�rio p�blico est�tico, publicado pelo GitHub Pages.
- `api/`: API ASP.NET Core 10 hospedada na SmarterASP, respons�vel por validar e gravar as respostas.
- `database/`: script para criar a tabela �nica e consultas para analisar as respostas.

O formul�rio n�o pede nome, e-mail ou telefone e a tabela n�o armazena IP. A hospedagem pode manter registros t�cnicos de acesso conforme a configura��o da conta. O campo aberto orienta a pessoa a n�o informar dados pessoais. A API aplica valida��o de entrada, limite de tamanho, honeypot e limita��o b�sica de tentativas por IP em mem�ria. O IP � usado somente para aplicar o limite durante a execu��o da API.

## 1. Usar o banco MSSQL existente

1. No painel SmarterASP, entre em **Databases > MSSQL** e selecione o banco que aparece na imagem enviada.
2. Abra o SQL Server Management Studio/SQL Studio e execute `database/create-feedback-table.sql` nesse banco. O script cria somente a tabela da pesquisa; n�o � necess�rio criar outro banco.
3. Guarde os dados da conex�o. N�o coloque a senha em arquivos que ser�o enviados ao GitHub.

O script cria somente `dbo.MelvLiturgiaFeedback` e um �ndice por data. `MelvLiturgiaFeedback` tamb�m � apenas o nome da chave de configura��o da API; n�o precisa existir um banco com esse nome. A connection string dessa chave aponta para o banco MSSQL existente. O banco continua acess�vel para consulta pelo SQL Studio. `database/analysis-queries.sql` cont�m consultas iniciais para contar plataformas, frequ�ncia e dificuldades, al�m de ler as respostas abertas.

## 2. Preparar a API para SmarterASP

1. `api/appsettings.json` j� permite a origem `https://movimentoluzvida.github.io`. Se usar um dom�nio pr�prio para o GitHub Pages, troque pelo dom�nio de origem, sem caminho do reposit�rio e sem barra final.
2. Configure a connection string no ambiente do aplicativo da SmarterASP usando a vari�vel `ConnectionStrings__MelvLiturgiaFeedback`. Use o exemplo ASP.NET de **Databases > MSSQL > Connection String** e substitua a senha no painel, nunca no GitHub.
3. Publique o projeto contido em `api/` como ASP.NET Core 10. Pelo terminal, a partir dessa pasta, execute `dotnet publish -c Release -o publish`. Envie o conte�do de `api/publish/` para o aplicativo da hospedagem, ou publique pelo Visual Studio/Web Deploy.
4. Se o painel permitir, use para o usu�rio SQL da API somente a permiss�o de inserir registros nessa tabela. A consulta dos resultados pode ser feita pelo seu acesso administrativo no SQL Studio.
5. Anote a URL HTTPS da API. Teste `https://SUA-API/api/health` (API ativa) e `https://SUA-API/api/health/database` (conex�o ao banco). A segunda rota n�o retorna credenciais nem detalhes do servidor.

O nome exato da op��o para cadastrar vari�veis pode variar conforme a vers�o do painel e o tipo de plano. A SmarterASP documenta o uso de vari�veis do ambiente no n�vel do Application Pool.

## 3. Apontar o formul�rio para a API

Edite `web/config.js` e substitua o endere�o de exemplo pelo endere�o HTTPS da API:

```js
window.ESCUTA_API_BASE_URL = "https://SUA-API";
```

N�o acrescente `/api/v1/responses`; o formul�rio adiciona esse caminho automaticamente.

## 4. Publicar no GitHub Pages

1. Crie um reposit�rio GitHub para este projeto e envie os arquivos.
2. Abra **Settings > Pages** e escolha publica��o por **GitHub Actions**.
3. Envie `web/config.js` j� com a URL HTTPS da API. O workflow em `.github/workflows/pages.yml` publica a pasta `web/` quando houver altera��o em `main`.
4. Aguarde a execu��o em **Actions** e copie o endere�o publicado. Se usar um dom�nio pr�prio, use a origem desse dom�nio na configura��o de CORS da API.

Como o reposit�rio pode ser p�blico, nunca coloque nele a senha do banco, connection string, tokens ou outras credenciais. O endere�o da API � p�blico e n�o � um segredo.

## 5. Conferir uma resposta

Depois de publicar as duas partes, abra o formul�rio, envie uma resposta de teste e confirme no SQL Studio:

```sql
SELECT TOP (10) SubmittedAtUtc, PlatformsJson, ContentTypesJson, Frequency,
       DifficultiesJson, Example, WorksWell, Suggestion
FROM dbo.MelvLiturgiaFeedback
ORDER BY SubmittedAtUtc DESC;
```

Remova a resposta de teste antes de divulgar a pesquisa, se quiser manter os resultados somente com participantes reais.

## Limites conhecidos

- A limita��o por IP � em mem�ria e reinicia junto com a API; serve como prote��o b�sica para esta coleta de baixo volume.
- O painel de confer�ncia fica na p�gina: clique sete vezes no logo MELV e entre com a senha cadastrada em dbo.UserConference. A tela permite buscar, editar e excluir respostas.
- O endere�o final do GitHub Pages, o dom�nio/subdom�nio da API e os dados da connection string dependem da sua conta, ent�o devem ser preenchidos no seu painel.


## Acesso ao painel de confer�ncia

O script SQL cria a tabela dbo.UserConference com um �nico campo, Password. Cadastre a senha longa manualmente no SSMS com:

    INSERT INTO dbo.UserConference ([Password]) VALUES (N'SUA-SENHA-LONGA');

A senha fica em texto simples nessa tabela, conforme definido para esta pesquisa. N�o a inclua no GitHub. Ap�s atualizar o servidor da API, clique sete vezes no logo MELV, informe a senha e use os bot�es Editar ou Excluir. O painel tamb�m inclui busca; a sess�o dura at� oito horas e o bot�o Sair encerra o token.


