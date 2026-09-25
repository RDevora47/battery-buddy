namespace BatteryBuddy.Pet.Scene;

/// <summary>Pickable: sage, clickable. Current: the channel the mouse is on, greyed. Empty: nothing paired, hollow.</summary>
public enum BubbleLook { Pickable, Current, Empty }

/// <summary>One channel bubble this frame: top-left in art pixels; Hovered lifts a pickable one by a pixel.</summary>
public sealed record ChannelBubble(int Number, BubbleLook Look, PixelPoint At, bool Hovered = false);

/// <summary>The channel picker this frame. Mouse: the mouse sprite's top-left; Trail: dotted lines from it to each bubble.</summary>
public sealed record PickerDrawing(IReadOnlyList<ChannelBubble> Bubbles, PixelPoint Mouse, bool Trail);
