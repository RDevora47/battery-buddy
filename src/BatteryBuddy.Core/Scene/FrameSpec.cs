namespace BatteryBuddy.Core.Scene;

public sealed record Overlay(string Sprite, int X, int Y);

/// <summary>What to draw this frame. CriticalVisible=false hides sprites of devices at ≤ 10 %.</summary>
public sealed record FrameSpec(string BodySprite, int BodyDy, IReadOnlyList<Overlay> Overlays, bool CriticalVisible);
