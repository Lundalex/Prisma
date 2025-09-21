using UnityEngine;

public class ForceAnimatorActive : MonoBehaviour
{
    public Animator animator;

    void Update()
    {
        if (animator) animator.enabled = true;
    }
}