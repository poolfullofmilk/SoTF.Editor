using SoTF.Editor.Data.Models;

namespace SoTF.Editor.Data.Helpers;

public static class Teleporter
{
    private const int TeleportYoffset = 3;

    public static void MovePlayerToPos(ref Position player, ref Position target)
    {
        player = target.WithoutOffset();
        target = target.WithYOffset(TeleportYoffset);
    }

    public static Position MoveToPos(Position targetPos)
    {
        return targetPos.WithYOffset(TeleportYoffset);
    }
}
