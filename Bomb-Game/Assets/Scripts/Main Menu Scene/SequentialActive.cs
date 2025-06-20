using UnityEngine;
using System.Collections;

public class SequentialActivator : MonoBehaviour
{
    public GameObject[] objects;

    public float activeDuration = 1f;
    public float delayBetween = 0.5f;
    public bool loopForever = true;

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

        foreach (GameObject obj in objects)
        {
            if (obj != null)
                obj.SetActive(false);
        }
    }
}
