using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Stores formulas that the player has successfully brewed.</summary>
public static class PotiondexProgress
{
    private const string SaveKey = "Potiondex.Brewed";
    private const string SeenKey = "Potiondex.Revealed";
    private static readonly HashSet<string> Brewed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Revealed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> TestReveal = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static bool loaded;
    public static event Action Changed;

    public static bool HasBrewed(string recipeName)
    {
        Load();
        return Brewed.Contains(recipeName);
    }

    public static void MarkBrewed(string recipeName)
    {
        if (string.IsNullOrWhiteSpace(recipeName)) return;
        Load();
        if (!Brewed.Add(recipeName)) return;
        PlayerPrefs.SetString(SaveKey, string.Join("|", Brewed));
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static bool HasBeenRevealed(string recipeName) { Load(); return Revealed.Contains(recipeName); }
    public static bool HasSavedProgress { get { Load(); return Brewed.Count > 0 || Revealed.Count > 0; } }
    public static void MarkRevealed(string recipeName) { Load(); if (Revealed.Add(recipeName)) Save(); }
    public static void ClearAll() { Load(); Brewed.Clear(); Revealed.Clear(); Save(); Changed?.Invoke(); }
    public static void UnlockForTesting(string recipeName)
    {
        MarkBrewed(recipeName);
        if (string.Equals(recipeName, "Health", StringComparison.OrdinalIgnoreCase))
        {
            RequestTestReveal("Regeneration");
            RequestTestReveal("Antidote");
        }
    }
    public static void RequestTestReveal(string recipeName) { if (!string.IsNullOrWhiteSpace(recipeName)) { TestReveal.Add(recipeName); Changed?.Invoke(); } }
    public static bool IsTestRevealRequested(string recipeName) => TestReveal.Contains(recipeName);
    public static void CompleteTestReveal(string recipeName) => TestReveal.Remove(recipeName);

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        foreach (string name in PlayerPrefs.GetString(SaveKey, string.Empty).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            Brewed.Add(name);
        foreach (string name in PlayerPrefs.GetString(SeenKey, string.Empty).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            Revealed.Add(name);
    }

    private static void Save() { PlayerPrefs.SetString(SaveKey, string.Join("|", Brewed)); PlayerPrefs.SetString(SeenKey, string.Join("|", Revealed)); PlayerPrefs.Save(); }
}
