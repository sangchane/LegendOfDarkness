using Godot;

namespace MobileSmoke;

public partial class Main : Control
{
    private const string SuccessMarker = "MOBILE_SMOKE_OK";

    public override void _Ready()
    {
        string platform = OS.GetName();
        string runtime = System.Environment.Version.ToString();
        string message = $"{SuccessMarker} | Godot C# | {platform} | .NET {runtime}";

        GetNode<Label>("Center/Panel/Content/Status").Text = message;
        GD.Print(message);
    }
}
