namespace TravelPlanning.UI.Editor
{
    /// <summary>Compatibility entry point; always opens the team's actual login design.</summary>
    public static class LoginPageSetup
    {
#region CreateScene
        public static void CreateScene() => AuthSetup.Prepare();
#endregion
    }
}
