namespace BatteryBuddy.Core.Scene;

public sealed record Overlay(string Sprite, int X, int Y);

/// <summary>How a device shows its charge: a colored ring around its sprite, or the segmented bar.</summary>
public enum BatteryStyle { Outline, Bar }

/// <summary>What to draw this frame. CriticalVisible=false hides sprites of devices at ≤ 10 %.</summary>
public sealed record FrameSpec(string BodySprite, int BodyDy, IReadOnlyList<Overlay> Overlays, bool CriticalVisible);
