using UnityEngine;

[ExecuteAlways]
public class ContainerWithTrim : MonoBehaviour
{
    [SerializeField] private RectTransform otherContainer;
    [SerializeField] private RectTransform trimContainer;
    [SerializeField] private RectTransform innerContainer;

    private Vector2 containerSpacing = new(10, 10);

    void Update()
    {
        containerSpacing = new(10, 10);
        if (!Application.isPlaying)
        {
            if (otherContainer != null && trimContainer != null && innerContainer != null)
            {
                trimContainer.sizeDelta = otherContainer.sizeDelta - containerSpacing;
                innerContainer.sizeDelta = otherContainer.sizeDelta - 2 * containerSpacing;
            }
        }
    }

    void Start()
    {
        containerSpacing = new(10, 10);
        if (otherContainer != null && trimContainer != null && innerContainer != null)
        {
            trimContainer.sizeDelta = otherContainer.sizeDelta - containerSpacing;
            innerContainer.sizeDelta = otherContainer.sizeDelta - 2 * containerSpacing;
        }
    }
}