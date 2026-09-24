namespace BatteryBuddy.Pet.Scene;

public sealed record Overlay(string Sprite, int X, int Y);

/// <summary>How a device shows its charge: a colored ring around its sprite, the segmented bar, or a mini battery beside it.</summary>
public enum BatteryStyle { Outline, Bar, Gauge }

/// <summary>
/// What to draw this frame. CriticalDx shifts sprites of devices at ≤ 10 % sideways (the pet shaking them).
/// Gills is drawn over the body; Fade (0..1) desaturates body, gills and paws toward grey.
/// PawLeftDy / PawRightDy push a paw down while it taps a key or clicks; ClickDy also takes the mouse along.
/// RightPawOnMouse moves the right paw from the body to the skin's MousePaw spot.
/// Tail is the tail pose, drawn only for skins that have a Tail spot.
/// BodyDx moves the pet sideways with everything that follows its body (a perched pet hopping key to key).
/// </summary>
public sealed record FrameSpec(
    string BodySprite, int BodyDy, IReadOnlyList<Overlay> Overlays,
    int CriticalDx = 0, string? Gills = null, double Fade = 0, int PawLeftDy = 0, int PawRightDy = 0, int ClickDy = 0, bool RightPawOnMouse = false,
    string? Tail = null, int BodyDx = 0)
{
    public int PawDy(Paw paw) => paw == Paw.Left ? PawLeftDy : PawRightDy;
}
