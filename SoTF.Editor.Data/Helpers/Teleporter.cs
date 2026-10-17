using SoTF.Editor.Data.Models;

namespace SoTF.Editor.Data.Helpers;

public static class Teleporter
{
    // Lands A Little Above, Never Inside The Ground
    private const int TeleportYOffset = 3;

    public static Position MoveToPos(Position targetPos)
    {
        return targetPos.WithYOffset(TeleportYOffset);
    }
}
