
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewScreamer",
    menuName = "Horror/Screamer Data"
)]
public class ScreamerData : ScriptableObject
{
    [Header("Identification")]
    public string screamerID;

    [Header("Screamer")]
    public GameObject screamerPrefab;

    [Header("Audio")]
    public AudioClip screamAudio;

    [Header("Settings")]
    public float duration = 2f;
    public float cooldown = 3f;
}
