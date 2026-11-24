
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ScreamerManager : MonoBehaviour
{
    public static ScreamerManager Instance { get; private set; }

    [Header("Available Screamers")]
    [SerializeField] private List<ScreamerData> screamers = new();

    private Dictionary<string, ScreamerData> screamerDatabase;

    private bool isPlaying;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        screamerDatabase = new Dictionary<string, ScreamerData>();

        foreach (ScreamerData screamer in screamers)
        {
            if (screamer == null) continue;

            if (string.IsNullOrWhiteSpace(screamer.screamerID))
            {
                Debug.LogWarning("Screamer without ID!");
                continue;
            }

            if (!screamerDatabase.TryAdd(
                screamer.screamerID, screamer))
            {
                Debug.LogWarning(
                    $"Duplicate Screamer ID: {screamer.screamerID}"
                );
            }
        }
    }

    public void PlayScreamer(string screamerID)
    {
        if (isPlaying)
        {
            Debug.Log("A screamer is already playing.");
            return;
        }

        if (!screamerDatabase.TryGetValue(
            screamerID, out ScreamerData data))
        {
            Debug.LogWarning(
                $"Screamer not found: {screamerID}"
            );
            return;
        }

        StartCoroutine(PlaySequence(data));
    }

    private IEnumerator PlaySequence(ScreamerData data)
    {
        isPlaying = true;

        GameObject instance = Instantiate(
            data.screamerPrefab
        );

        AudioSource.PlayClipAtPoint(
            data.screamAudio,
            transform.position
        );

        yield return new WaitForSeconds(data.duration);

        Destroy(instance);

        yield return new WaitForSeconds(data.cooldown);

        isPlaying = false;
    }
}
