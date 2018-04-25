namespace Inedo.Extensions
{
    internal static class Extension
    {
        public static string Version => typeof(Extension).Assembly.GetName().Version.ToString();
    }
}
