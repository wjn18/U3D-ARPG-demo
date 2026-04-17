using UnityEngine;

public class CombatAudioController : MonoBehaviour
{
    [Header("Refs")]
    public AudioSource audioSource;

    [Header("Attack")]
    public AudioClip[] attackVoiceClips;
    public AudioClip[] attackSwingClips;
    public AudioClip[] attackHitClips;
    public AudioClip[] attackMissClips;

    [Header("Defense / Hurt")]
    public AudioClip[] blockedHitClips;
    public AudioClip[] hurtClips;

    [Header("Voice")]
    public AudioClip[] dieVoiceClips;
    public AudioClip[] berserkVoiceClips;

    void Reset()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public void PlayAttackStart()
    {
        PlayRandomClip(attackVoiceClips);
        PlayRandomClip(attackSwingClips);
    }

    public void PlayAttackStart(AudioClip[] voiceClips, AudioClip[] swingClips)
    {
        PlayRandomClip(voiceClips);
        PlayRandomClip(swingClips);
    }

    public void PlayAttackHit()
    {
        PlayRandomClip(attackHitClips);
    }

    public void PlayAttackHit(AudioClip[] clips)
    {
        PlayRandomClip(clips);
    }

    public void PlayAttackMiss()
    {
        PlayRandomClip(attackMissClips);
    }

    public void PlayAttackMiss(AudioClip[] clips)
    {
        PlayRandomClip(clips);
    }

    public void PlayBlockedHit()
    {
        PlayRandomClip(blockedHitClips);
    }

    public void PlayHurt()
    {
        PlayRandomClip(hurtClips);
    }

    public void PlayHurt(AudioClip[] clips)
    {
        PlayRandomClip(clips);
    }

    public void PlayDieVoice()
    {
        PlayRandomClip(dieVoiceClips);
    }

    public void PlayDieVoice(AudioClip[] clips)
    {
        PlayRandomClip(clips);
    }

    public void PlayBerserkVoice()
    {
        PlayRandomClip(berserkVoiceClips);
    }

    void PlayRandomClip(AudioClip[] clips)
    {
        if (audioSource == null || clips == null || clips.Length == 0)
            return;

        AudioClip clip = GetRandomClip(clips);
        if (clip == null)
            return;

        audioSource.PlayOneShot(clip);
    }

    AudioClip GetRandomClip(AudioClip[] clips)
    {
        int startIndex = Random.Range(0, clips.Length);
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[(startIndex + i) % clips.Length];
            if (clip != null)
                return clip;
        }

        return null;
    }
}
