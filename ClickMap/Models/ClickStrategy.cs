namespace ClickMap.Models;

/// <summary>
/// How a click is delivered to the target point. Windows only clicks where the cursor
/// is, so the strategies differ in what they do about the cursor.
/// </summary>
public enum ClickStrategy
{
    /// <summary>
    /// Move the cursor to the target, click, and move it straight back — all in one
    /// input batch, so the round trip is over in well under a frame. The cursor ends
    /// where it started. Uses the normal input pipeline, so it works everywhere.
    /// </summary>
    RestoreCursor,

    /// <summary>
    /// Post the click straight to the window under the target point. The cursor never
    /// moves at all. Bypasses the input pipeline, so apps that read the real cursor
    /// position, games, and elevated windows may not respond.
    /// </summary>
    DirectToWindow,

    /// <summary>Move the cursor to the target and leave it there. The most compatible.</summary>
    MoveCursor,
}
