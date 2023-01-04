﻿namespace ImageFakeScraperExample;

internal class Settings
{
    // Google
    public static bool GoogleRun { get; set; } = true;

    // DuckduckGO
    public static bool DuckduckGORun { get; set; } = false;

    // Brave
    public static bool BraveRun { get; set; } = false;

    // OpenVerse
    public static bool OpenVerseRun { get; set; } = true;

    // Bing
    public static bool BingRun { get; set; } = true;

    // Yahoo
    public static bool YahooRun { get; set; } = true;

    // GettyImage
    public static bool GettyImageRun { get; set; } = true;

    // Immerse
    public static bool ImmerseRun { get; set; } = true;

    // EveryPixel
    public static bool EveryPixelRun { get; set; } = true;

    // Redis image push
    public static int stopAfter { get; } = 10;
    public static int restartAfter { get; set; } = 9;
    public static bool useMongoDB { get; set; } = true;
    public static bool PrintLog { get; set; } = true;

    // Main 
    public static bool PrintLogMain { get; set; } = true;
    public static bool GetNewTagGoogle { get; set; } = false;
    public static bool GetNewTagBing { get; set; } = false;


}
