using System.Collections.Generic;

public readonly struct ComboMatchResult
{
    public ComboDefinition Definition { get; }
    public int StartIndex { get; }
    public int EndIndex { get; }
    public float StartTimestamp { get; }
    public float EndTimestamp { get; }
    public int Length => Definition != null ? Definition.Length : 0;
    public bool IsValid => Definition != null;

    public ComboMatchResult(
        ComboDefinition definition,
        int startIndex,
        int endIndex,
        float startTimestamp,
        float endTimestamp)
    {
        Definition = definition;
        StartIndex = startIndex;
        EndIndex = endIndex;
        StartTimestamp = startTimestamp;
        EndTimestamp = endTimestamp;
    }
}

public static class ComboMatcher
{
    public static bool TryFindBestMatch(
        IReadOnlyList<CombatBufferedInput> inputHistory,
        IList<ComboDefinition> comboDefinitions,
        float minEndTimestampExclusive,
        out ComboMatchResult bestMatch)
    {
        bestMatch = default;

        if (inputHistory == null || comboDefinitions == null)
            return false;

        bool found = false;

        for (int comboIndex = 0; comboIndex < comboDefinitions.Count; comboIndex++)
        {
            ComboDefinition definition = comboDefinitions[comboIndex];
            if (definition == null || !definition.IsValid)
                continue;

            for (int startIndex = 0; startIndex <= inputHistory.Count - definition.Length; startIndex++)
            {
                if (!SequenceMatches(inputHistory, startIndex, definition))
                    continue;

                int endIndex = startIndex + definition.Length - 1;
                float endTimestamp = inputHistory[endIndex].timestamp;
                if (endTimestamp <= minEndTimestampExclusive)
                    continue;

                ComboMatchResult candidate = new ComboMatchResult(
                    definition,
                    startIndex,
                    endIndex,
                    inputHistory[startIndex].timestamp,
                    endTimestamp);

                if (!found || IsHigherPriority(candidate, bestMatch))
                {
                    bestMatch = candidate;
                    found = true;
                }
            }
        }

        return found;
    }

    static bool SequenceMatches(
        IReadOnlyList<CombatBufferedInput> inputHistory,
        int startIndex,
        ComboDefinition definition)
    {
        if (definition == null || !definition.IsValid)
            return false;

        if (startIndex < 0 || startIndex + definition.Length > inputHistory.Count)
            return false;

        for (int i = 0; i < definition.Length; i++)
        {
            CombatBufferedInput bufferedInput = inputHistory[startIndex + i];
            if (bufferedInput.inputType != definition.InputSequence[i])
                return false;

            if (i <= 0)
                continue;

            float gap = bufferedInput.timestamp - inputHistory[startIndex + i - 1].timestamp;
            if (gap > definition.MaxAllowedGap)
                return false;
        }

        return true;
    }

    static bool IsHigherPriority(ComboMatchResult candidate, ComboMatchResult currentBest)
    {
        if (candidate.Length != currentBest.Length)
            return candidate.Length > currentBest.Length;

        if (candidate.Definition.Priority != currentBest.Definition.Priority)
            return candidate.Definition.Priority > currentBest.Definition.Priority;

        return candidate.EndTimestamp > currentBest.EndTimestamp;
    }
}
