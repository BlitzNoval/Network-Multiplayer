using UnityEngine;
using System.Collections;

public class SequentialActivator : MonoBehaviour
{
    [Header("Objects to Activate In Order")]
    public GameObject[] objects;

    [Header("Timing Settings")]
    public float activeDuration = 1f;     // How long each object stays active
    public float delayBetween = 0.5f;     // Time between one turning off and the next turning on
    public bool loopForever = true;       // Should the sequence repeat forever?

    private Coroutine sequenceCoroutine;

    private void Start()
    {
        sequenceCoroutine = StartCoroutine(ActivateObjectsSequentially());
    }

    private IEnumerator ActivateObjectsSequentially()
    {
        do
        {
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject obj = objects[i];

                if (obj != null)
                {
                    obj.SetActive(true);
                    yield return new WaitForSeconds(activeDuration);
                    obj.SetActive(false);
                    yield return new WaitForSeconds(delayBetween);
                }
            }
        } while (loopForever);
    }

    public void StopSequence()
    {
        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }

        // Optional: turn everything off when stopped
        foreach (GameObject obj in objects)
        {
            if (obj != null)
                obj.SetActive(false);
        }
    }
}
