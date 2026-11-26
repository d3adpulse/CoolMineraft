using Godot;

namespace VoxelCraft.Player;

/// <summary>Рамка вокруг блока под прицелом — как в Minecraft, сильно помогает целиться.</summary>
public partial class BlockHighlight : MeshInstance3D
{
    private const float Half = 0.502f;

    public override void _Ready()
    {
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.02f, 0.02f, 0.02f, 0.9f),
            Transparency = BaseMaterial3D.TransparencyEnum.Disabled,
        };

        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);

        // 12 рёбер куба.
        AddLine(mesh, new Vector3(-Half, -Half, -Half), new Vector3(Half, -Half, -Half));
        AddLine(mesh, new Vector3(Half, -Half, -Half), new Vector3(Half, -Half, Half));
        AddLine(mesh, new Vector3(Half, -Half, Half), new Vector3(-Half, -Half, Half));
        AddLine(mesh, new Vector3(-Half, -Half, Half), new Vector3(-Half, -Half, -Half));

        AddLine(mesh, new Vector3(-Half, Half, -Half), new Vector3(Half, Half, -Half));
        AddLine(mesh, new Vector3(Half, Half, -Half), new Vector3(Half, Half, Half));
        AddLine(mesh, new Vector3(Half, Half, Half), new Vector3(-Half, Half, Half));
        AddLine(mesh, new Vector3(-Half, Half, Half), new Vector3(-Half, Half, -Half));

        AddLine(mesh, new Vector3(-Half, -Half, -Half), new Vector3(-Half, Half, -Half));
        AddLine(mesh, new Vector3(Half, -Half, -Half), new Vector3(Half, Half, -Half));
        AddLine(mesh, new Vector3(Half, -Half, Half), new Vector3(Half, Half, Half));
        AddLine(mesh, new Vector3(-Half, -Half, Half), new Vector3(-Half, Half, Half));

        mesh.SurfaceEnd();
        Mesh = mesh;
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        Visible = false;
    }

    private static void AddLine(ImmediateMesh mesh, Vector3 from, Vector3 to)
    {
        mesh.SurfaceAddVertex(from);
        mesh.SurfaceAddVertex(to);
    }

    public void ShowAt(Vector3I cell)
    {
        GlobalPosition = new Vector3(cell.X + 0.5f, cell.Y + 0.5f, cell.Z + 0.5f);
        Visible = true;
    }

    public void HideBox() => Visible = false;
}
