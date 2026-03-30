namespace API;

public static class DestinoTipos
{
    public const string S3 = "S3";
    public const string GoogleDrive = "GoogleDrive";

    public static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        S3,
        GoogleDrive
    };
}
