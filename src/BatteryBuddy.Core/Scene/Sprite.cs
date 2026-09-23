namespace BatteryBuddy.Core.Scene;

/// <summary>Pixels are ARGB (0xAARRGGBB); 0 is transparent.</summary>
public sealed record Sprite(string Name, int Width, int Height, uint[] Pixels);
