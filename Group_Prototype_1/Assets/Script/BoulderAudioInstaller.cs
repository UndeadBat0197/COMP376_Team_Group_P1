
using UnityEngine;
using System.Collections;

public class BoulderAudioInstaller : MonoBehaviour
{
    [Header("Boulder Sound Effects")]
    public AudioClip rollingSound;
    public AudioClip impactSound;

    [Header("Sound Volume")]
    [Range(0f, 1f)]
    public float rollingVolume = 0.4f;

    [Range(0f, 1f)]
    public float impactVolume = 0.7f;

    [Header("Detection Settings")]
    public float scanInterval = 0.5f;

    private Coroutine scanCoroutine;

    void OnEnable()
    {
        scanCoroutine =
            StartCoroutine(FindBoulders());
    }

    void OnDisable()
    {
        if (scanCoroutine != null)
        {
            StopCoroutine(scanCoroutine);
            scanCoroutine = null;
        }
    }

    IEnumerator FindBoulders()
    {
        while (true)
        {
            Rigidbody[] bodies =
                FindObjectsOfType<Rigidbody>();

            foreach (Rigidbody body in bodies)
            {
                if (body == null)
                    continue;

                GameObject obj = body.gameObject;

                // Only select Boulder objects
                if (!obj.name.StartsWith("Boulder"))
                    continue;

                BoulderSound sound =
                    obj.GetComponent<BoulderSound>();

                // Add the component only once
                if (sound == null)
                {
                    sound =
                        obj.AddComponent<BoulderSound>();

                    Debug.Log(
                        "Boulder audio installed: " +
                        obj.name
                    );
                }

                // Assign audio clips
                sound.rollingSound = rollingSound;
                sound.impactSound = impactSound;

                // Assign volumes
                sound.rollingVolume =
                    rollingVolume;

                sound.impactVolume =
                    impactVolume;
            }

            yield return new WaitForSeconds(
                Mathf.Max(0.1f, scanInterval)
            );
        }
    }
}
