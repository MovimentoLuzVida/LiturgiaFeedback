using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 20_000);

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()?
    .Where(origin => Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
                     (uri.Scheme == Uri.UriSchemeHttps || uri.Host is "localhost" or "127.0.0.1"))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray() ?? [];

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0)
        policy.WithOrigins(allowedOrigins).WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS").WithHeaders("Content-Type", "Authorization");
}));

var app = builder.Build();
app.UseHttpsRedirection();
app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/health/database", CheckDatabase);

app.MapPost("/api/v1/responses", SubmitResponse);
app.MapPost("/api/v1/conference/login", ConferenceLogin);
app.MapPost("/api/v1/conference/logout", ConferenceLogout);
app.MapGet("/api/v1/conference/responses", ListResponses);
app.MapPut("/api/v1/conference/responses/{id:guid}", UpdateResponse);
app.MapDelete("/api/v1/conference/responses/{id:guid}", DeleteResponse);

app.Run();

static async Task<IResult> CheckDatabase(IConfiguration configuration, ILogger<Program> logger, CancellationToken cancellationToken)
{
    var connectionString = configuration.GetConnectionString("MelvLiturgiaFeedback");
    if (string.IsNullOrWhiteSpace(connectionString))
        return Results.Problem("A conex�o com o banco ainda n�o est� configurada.", statusCode: StatusCodes.Status503ServiceUnavailable);

    try
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return Results.Ok(new { status = "ok", database = "available" });
    }
    catch (SqlException exception)
    {
        logger.LogError(exception, "N�o foi poss�vel verificar a conex�o com o banco.");
        return Results.Problem("O banco n�o est� dispon�vel.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

static async Task<IResult> SubmitResponse(SurveyResponseRequest request, IConfiguration configuration, ILogger<Program> logger, CancellationToken cancellationToken)
{
    // Campo invis�vel usado como honeypot. Responder como sucesso sem gravar reduz spam automatizado.
    if (!string.IsNullOrWhiteSpace(request.Website))
        return Results.Accepted();

    var errors = SurveyValidation.Validate(request);
    if (errors.Count > 0)
        return Results.ValidationProblem(errors);

    var connectionString = configuration.GetConnectionString("MelvLiturgiaFeedback");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        logger.LogError("A connection string MelvLiturgiaFeedback n�o foi configurada.");
        return Results.Problem("A coleta ainda n�o est� configurada.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.MelvLiturgiaFeedback
                (ResponseId, PlatformsJson, OtherPlatform, ContentTypesJson, Frequency, DifficultiesJson, Example, WorksWell, Suggestion)
            VALUES
                (@ResponseId, @PlatformsJson, @OtherPlatform, @ContentTypesJson, @Frequency, @DifficultiesJson, @Example, @WorksWell, @Suggestion);
            """;

        command.Parameters.Add("@ResponseId", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        command.Parameters.Add("@PlatformsJson", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(request.Platforms ?? []);
        command.Parameters.Add("@OtherPlatform", SqlDbType.NVarChar, 100).Value = DbValue(request.OtherPlatform);
        command.Parameters.Add("@ContentTypesJson", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(request.ContentTypes ?? []);
        command.Parameters.Add("@Frequency", SqlDbType.NVarChar, 40).Value = request.Frequency ?? "";
        command.Parameters.Add("@DifficultiesJson", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(request.Difficulties ?? []);
        command.Parameters.Add("@Example", SqlDbType.NVarChar, 1500).Value = DbValue(request.Example);
        command.Parameters.Add("@WorksWell", SqlDbType.NVarChar, 1000).Value = DbValue(request.WorksWell);
        command.Parameters.Add("@Suggestion", SqlDbType.NVarChar, 1000).Value = DbValue(request.Suggestion);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return Results.Created("/api/v1/responses", new { received = true });
    }
    catch (SqlException exception)
    {
        logger.LogError(exception, "Falha ao persistir resposta an�nima da pesquisa.");
        return Results.Problem("N�o foi poss�vel registrar a resposta agora. Tente novamente mais tarde.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

static object DbValue(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

static async Task<IResult> ConferenceLogin(ConferenceLoginRequest request, IConfiguration configuration, CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 4000)
        return Results.Unauthorized();
    var connectionString = configuration.GetConnectionString("MelvLiturgiaFeedback");
    if (string.IsNullOrWhiteSpace(connectionString))
        return Results.Problem("A coleta ainda n�o est� configurada.", statusCode: StatusCodes.Status503ServiceUnavailable);

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync(cancellationToken);
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT TOP (1) [Password] FROM dbo.UserConference;";
    var storedPassword = await command.ExecuteScalarAsync(cancellationToken) as string;
    if (storedPassword is null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(storedPassword), Encoding.UTF8.GetBytes(request.Password)))
        return Results.Unauthorized();

    var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    var expires = DateTimeOffset.UtcNow.AddHours(8);
    ConferenceAuth.Sessions[token] = expires;
    return Results.Ok(new { token, expiresAtUtc = expires });
}

static bool IsConferenceAuthorized(HttpRequest request)
{
    var header = request.Headers.Authorization.ToString();
    if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
    var token = header[7..].Trim();
    if (!ConferenceAuth.Sessions.TryGetValue(token, out var expiresAt)) return false;
    if (expiresAt > DateTimeOffset.UtcNow) return true;
    ConferenceAuth.Sessions.TryRemove(token, out _);
    return false;
}

static IResult ConferenceLogout(HttpRequest request)
{
    var header = request.Headers.Authorization.ToString();
    if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        ConferenceAuth.Sessions.TryRemove(header[7..].Trim(), out _);
    return Results.NoContent();
}

static async Task<IResult> ListResponses(HttpRequest request, IConfiguration configuration, CancellationToken cancellationToken)
{
    if (!IsConferenceAuthorized(request)) return Results.Unauthorized();
    var connectionString = configuration.GetConnectionString("MelvLiturgiaFeedback");
    if (string.IsNullOrWhiteSpace(connectionString)) return Results.Problem("A coleta ainda n�o est� configurada.", statusCode: StatusCodes.Status503ServiceUnavailable);
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync(cancellationToken);
    await using var command = connection.CreateCommand();
    command.CommandText = """
        SELECT ResponseId, SubmittedAtUtc, PlatformsJson, OtherPlatform, ContentTypesJson,
               Frequency, DifficultiesJson, Example, WorksWell, Suggestion
        FROM dbo.MelvLiturgiaFeedback ORDER BY SubmittedAtUtc DESC;
        """;
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    var items = new List<ConferenceResponse>();
    while (await reader.ReadAsync(cancellationToken))
    {
        items.Add(new ConferenceResponse(
            reader.GetGuid(0), reader.GetDateTime(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9)));
    }
    return Results.Ok(items);
}

static async Task<IResult> UpdateResponse(Guid id, ConferenceResponseUpdate request, HttpRequest httpRequest, IConfiguration configuration, CancellationToken cancellationToken)
{
    if (!IsConferenceAuthorized(httpRequest)) return Results.Unauthorized();
    var errors = SurveyValidation.Validate(new SurveyResponseRequest { Platforms = request.Platforms, OtherPlatform = request.OtherPlatform,
        ContentTypes = request.ContentTypes, Frequency = request.Frequency, Difficulties = request.Difficulties,
        Example = request.Example, WorksWell = request.WorksWell, Suggestion = request.Suggestion });
    if (errors.Count > 0) return Results.ValidationProblem(errors);
    var connectionString = configuration.GetConnectionString("MelvLiturgiaFeedback");
    if (string.IsNullOrWhiteSpace(connectionString)) return Results.Problem("A coleta ainda n�o est� configurada.", statusCode: StatusCodes.Status503ServiceUnavailable);
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync(cancellationToken);
    await using var command = connection.CreateCommand();
    command.CommandText = """
        UPDATE dbo.MelvLiturgiaFeedback SET PlatformsJson=@PlatformsJson, OtherPlatform=@OtherPlatform,
        ContentTypesJson=@ContentTypesJson, Frequency=@Frequency, DifficultiesJson=@DifficultiesJson,
        Example=@Example, WorksWell=@WorksWell, Suggestion=@Suggestion WHERE ResponseId=@ResponseId;
        """;
    command.Parameters.Add("@ResponseId", SqlDbType.UniqueIdentifier).Value = id;
    command.Parameters.Add("@PlatformsJson", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(request.Platforms);
    command.Parameters.Add("@OtherPlatform", SqlDbType.NVarChar, 100).Value = DbValue(request.OtherPlatform);
    command.Parameters.Add("@ContentTypesJson", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(request.ContentTypes);
    command.Parameters.Add("@Frequency", SqlDbType.NVarChar, 40).Value = request.Frequency!;
    command.Parameters.Add("@DifficultiesJson", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(request.Difficulties);
    command.Parameters.Add("@Example", SqlDbType.NVarChar, 1500).Value = DbValue(request.Example);
    command.Parameters.Add("@WorksWell", SqlDbType.NVarChar, 1000).Value = DbValue(request.WorksWell);
    command.Parameters.Add("@Suggestion", SqlDbType.NVarChar, 1000).Value = DbValue(request.Suggestion);
    return await command.ExecuteNonQueryAsync(cancellationToken) == 0 ? Results.NotFound() : Results.NoContent();
}

static async Task<IResult> DeleteResponse(Guid id, HttpRequest request, IConfiguration configuration, CancellationToken cancellationToken)
{
    if (!IsConferenceAuthorized(request)) return Results.Unauthorized();
    var connectionString = configuration.GetConnectionString("MelvLiturgiaFeedback");
    if (string.IsNullOrWhiteSpace(connectionString)) return Results.Problem("A coleta ainda n�o est� configurada.", statusCode: StatusCodes.Status503ServiceUnavailable);
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync(cancellationToken);
    await using var command = connection.CreateCommand();
    command.CommandText = "DELETE FROM dbo.MelvLiturgiaFeedback WHERE ResponseId=@ResponseId;";
    command.Parameters.Add("@ResponseId", SqlDbType.UniqueIdentifier).Value = id;
    return await command.ExecuteNonQueryAsync(cancellationToken) == 0 ? Results.NotFound() : Results.NoContent();
}

public sealed class SurveyResponseRequest
{
    public string[]? Platforms { get; init; } = [];
    public string? OtherPlatform { get; init; }
    public string[]? ContentTypes { get; init; } = [];
    public string? Frequency { get; init; } = "";
    public string[]? Difficulties { get; init; } = [];
    public string? Example { get; init; }
    public string? WorksWell { get; init; }
    public string? Suggestion { get; init; }
    public string? Website { get; init; }
}

public sealed class ConferenceLoginRequest { public string? Password { get; init; } }
public sealed class ConferenceResponseUpdate
{
    public string[] Platforms { get; init; } = [];
    public string? OtherPlatform { get; init; }
    public string[] ContentTypes { get; init; } = [];
    public string? Frequency { get; init; }
    public string[] Difficulties { get; init; } = [];
    public string? Example { get; init; }
    public string? WorksWell { get; init; }
    public string? Suggestion { get; init; }
}
public sealed record ConferenceResponse(Guid ResponseId, DateTime SubmittedAtUtc, string PlatformsJson, string? OtherPlatform,
    string ContentTypesJson, string Frequency, string DifficultiesJson, string? Example, string? WorksWell, string? Suggestion);

internal static class ConferenceAuth
{
    public static ConcurrentDictionary<string, DateTimeOffset> Sessions { get; } = new();
}

internal static class SurveyValidation
{
    private static readonly HashSet<string> PlatformValues = ["iliturgia", "outro-app", "site", "livro", "youtube", "nao-uso"];
    private static readonly HashSet<string> ContentValues = ["liturgia-diaria", "laudes", "tercia", "sexta", "nona", "vesperas", "completas", "oficio-leituras", "acompanho-outro"];
    private static readonly HashSet<string> FrequencyValues = ["diariamente", "algumas-vezes-semana", "semanalmente", "ocasionalmente", "estou-comecando"];
    private static readonly HashSet<string> DifficultyValues = ["achar-conteudo", "entender-ordem", "calendario", "navegacao", "leitura-celular", "offline", "conteudo-incompleto", "outra-dificuldade", "sem-dificuldades"];

    public static Dictionary<string, string[]> Validate(SurveyResponseRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        CheckSet("platforms", request.Platforms, PlatformValues, required: true, errors);
        CheckSet("contentTypes", request.ContentTypes, ContentValues, required: true, errors);
        CheckSet("difficulties", request.Difficulties, DifficultyValues, required: true, errors);
        if (request.Frequency is null || !FrequencyValues.Contains(request.Frequency)) errors["frequency"] = ["Selecione uma frequ�ncia v�lida."];
        var difficulties = request.Difficulties ?? [];
        if (difficulties.Contains("sem-dificuldades") && difficulties.Length > 1)
            errors["difficulties"] = ["A op��o sem dificuldades deve ser marcada sozinha."];
        CheckLength("otherPlatform", request.OtherPlatform, 100, errors);
        CheckLength("example", request.Example, 1500, errors);
        CheckLength("worksWell", request.WorksWell, 1000, errors);
        CheckLength("suggestion", request.Suggestion, 1000, errors);
        return errors;
    }

    private static void CheckSet(string key, string[]? values, HashSet<string> allowed, bool required, Dictionary<string, string[]> errors)
    {
        values ??= [];
        if ((required && values.Length == 0) || values.Length > allowed.Count || values.Any(value => string.IsNullOrWhiteSpace(value) || !allowed.Contains(value)) || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            errors[key] = ["Confira as op��es selecionadas e tente novamente."];
    }

    private static void CheckLength(string key, string? value, int max, Dictionary<string, string[]> errors)
    {
        if (value?.Length > max) errors[key] = [$"O campo deve ter no m�ximo {max} caracteres."];
    }
}

