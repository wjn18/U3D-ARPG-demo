using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CombatInputBuffer
{
    [SerializeField] private float bufferWindowSeconds = 1.5f;
    [SerializeField] private int maxStoredInputs = 8;
    [SerializeField] private List<CombatBufferedInput> history = new List<CombatBufferedInput>();
    [SerializeField] private List<CombatBufferedInput> pendingInputs = new List<CombatBufferedInput>();

    public IReadOnlyList<CombatBufferedInput> History => history;
    public IReadOnlyList<CombatBufferedInput> PendingInputs => pendingInputs;
    public float BufferWindowSeconds => bufferWindowSeconds;
    public int MaxStoredInputs => maxStoredInputs;

    public void Configure(float windowSeconds, int maxInputs)
    {
        bufferWindowSeconds = Mathf.Max(0.05f, windowSeconds);
        maxStoredInputs = Mathf.Max(1, maxInputs);
    }

    public void RecordInput(CombatInputType inputType, float timestamp)
    {
        CombatBufferedInput bufferedInput = new CombatBufferedInput(inputType, timestamp);
        history.Add(bufferedInput);
        pendingInputs.Add(bufferedInput);

        EnforceCapacity();
        Prune(timestamp);
    }

    public void Prune(float currentTime)
    {
        float oldestAllowedTimestamp = currentTime - bufferWindowSeconds;
        RemoveExpired(history, oldestAllowedTimestamp);
        RemoveExpired(pendingInputs, oldestAllowedTimestamp);
        EnforceCapacity();
    }

    public bool TryPeekPending(out CombatBufferedInput bufferedInput)
    {
        if (pendingInputs.Count > 0)
        {
            bufferedInput = pendingInputs[0];
            return true;
        }

        bufferedInput = default;
        return false;
    }

    public bool ConsumePending(CombatBufferedInput bufferedInput)
    {
        for (int i = 0; i < pendingInputs.Count; i++)
        {
            CombatBufferedInput candidate = pendingInputs[i];
            if (candidate.inputType != bufferedInput.inputType)
                continue;

            if (!Mathf.Approximately(candidate.timestamp, bufferedInput.timestamp))
                continue;

            pendingInputs.RemoveAt(i);
            return true;
        }

        return false;
    }

    public int ConsumePendingUpToTimestamp(float maxTimestamp, int maxCount)
    {
        int consumedCount = 0;
        int allowedCount = Mathf.Max(0, maxCount);

        for (int i = 0; i < pendingInputs.Count && consumedCount < allowedCount;)
        {
            if (pendingInputs[i].timestamp <= maxTimestamp)
            {
                pendingInputs.RemoveAt(i);
                consumedCount++;
                continue;
            }

            i++;
        }

        return consumedCount;
    }

    public int ConsumePendingSequence(IReadOnlyList<CombatInputType> sequence, float maxTimestamp)
    {
        if (sequence == null || sequence.Count == 0)
            return 0;

        for (int startIndex = 0; startIndex <= pendingInputs.Count - sequence.Count; startIndex++)
        {
            int endIndex = startIndex + sequence.Count - 1;
            if (pendingInputs[endIndex].timestamp > maxTimestamp)
                break;

            bool matches = true;
            for (int i = 0; i < sequence.Count; i++)
            {
                if (pendingInputs[startIndex + i].inputType == sequence[i])
                    continue;

                matches = false;
                break;
            }

            if (!matches)
                continue;

            pendingInputs.RemoveRange(startIndex, sequence.Count);
            return sequence.Count;
        }

        return 0;
    }

    public void ClearAll()
    {
        history.Clear();
        pendingInputs.Clear();
    }

    public void ClearPending()
    {
        pendingInputs.Clear();
    }

    static void RemoveExpired(List<CombatBufferedInput> inputs, float oldestAllowedTimestamp)
    {
        while (inputs.Count > 0 && inputs[0].timestamp < oldestAllowedTimestamp)
            inputs.RemoveAt(0);
    }

    void EnforceCapacity()
    {
        int capacity = Mathf.Max(1, maxStoredInputs);
        TrimToCapacity(history, capacity);
        TrimToCapacity(pendingInputs, capacity);
    }

    static void TrimToCapacity(List<CombatBufferedInput> inputs, int capacity)
    {
        while (inputs.Count > capacity)
            inputs.RemoveAt(0);
    }
}
