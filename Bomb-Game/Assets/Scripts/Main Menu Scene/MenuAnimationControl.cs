using UnityEngine;
using System.Collections;

public class MenuAnimationControl : MonoBehaviour
{
    public Animator redAnim;
    public Animator blueAnim;
    public Animator greenAnim;
    public Animator yellowAnim;

    [SerializeField] private float startWaitTime;
    [SerializeField] private float timeBetweenAnimations;

    private int redRand;
    private int blueRand;
    private int greenRand;
    private int yellowRand;
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        redAnim.SetInteger("chosenAnimation", 0);
        blueAnim.SetInteger("chosenAnimation", 0);
        greenAnim.SetInteger("chosenAnimation", 0);
        yellowAnim.SetInteger("chosenAnimation", 0);

        StartCoroutine(SwitchBetweenAnimations());

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private IEnumerator SwitchBetweenAnimations()
    {
        yield return new WaitForSeconds(startWaitTime);

        while (true)
        {
            int randomNumber1 = Random.Range(0, 2);
            int randomNumber2 = Random.Range(0, 2);
            int randomNumber3 = Random.Range(0, 2);
            int randomNumber4 = Random.Range(0, 2);

            redAnim.SetInteger("chosenAnimation", randomNumber1);
            blueAnim.SetInteger("chosenAnimation", randomNumber2);
            greenAnim.SetInteger("chosenAnimation", randomNumber3);
            yellowAnim.SetInteger("chosenAnimation", randomNumber4);

            if (randomNumber1 > 0)
            {
                yield return new WaitForSeconds(3f);
                redAnim.SetInteger("chosenAnimation", 0);
            }

            if (randomNumber2 > 0)
            {
                yield return new WaitForSeconds(3f);
                blueAnim.SetInteger("chosenAnimation", 0);
            }

            if (randomNumber3 > 0)
            {
                yield return new WaitForSeconds(3f);
                greenAnim.SetInteger("chosenAnimation", 0);
            }

            if (randomNumber4 > 0)
            {
                yield return new WaitForSeconds(3f);
                yellowAnim.SetInteger("chosenAnimation", 0);
            }

            yield return new WaitForSeconds(timeBetweenAnimations);

        }


    }
}
