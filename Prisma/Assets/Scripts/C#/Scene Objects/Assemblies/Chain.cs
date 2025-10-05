using UnityEngine;
using Resources2;
using PM = ProgramManager;
using System.Collections.Generic;

public class Chain : Assembly
{
    [Header("(Modify hook settings in the 'SettingsHook' rigid body child object)")]

    [Header("Chain Links")]
    public ColliderType linkColliderType;
    public MouseInteraction mouseInteraction;
    public int numLinks = 5;
    public float linkWidth = 10.0f;
    public float linkHeight = 20.0f;
    public float inBetweenLength = 5.0f;
    public float firstLinkMass = 500.0f;
    public float lastLinkMass = 1000.0f;

    [Header("References")]
    [SerializeField] private SceneRigidBody hook;
    [SerializeField] private GameObject linkPrefab;
    [SerializeField] private GameObject linksContainer;
    private List<GameObject> links = new();

    private void OnEnable()
    {
        PM.Instance.OnPreStart += AssemblyUpdate;
    }

    private void OnDestroy()
    {
        PM.Instance.OnPreStart -= AssemblyUpdate;
    }

    public override void AssemblyUpdate()
    {
        if (hook == null || linkPrefab == null || linksContainer == null)
        {
            Debug.LogWarning($"All references must be set in the inspector for Chain: {this.name}");
            return;
        }

        // Remove all invalid null links
        for (int i = links.Count - 1; i >= 0; i--)
        {
            if (links[i] == null) links.RemoveAt(i);
        }

        // Add existing children to the links list to avoid duplication
        foreach (Transform child in linksContainer.transform)
        {
            if (!links.Contains(child.gameObject))
            {
                links.Add(child.gameObject);
            }
        }

        // Activate required links and deactivate the unused ones
        for (int i = 0; i < links.Count; i++)
        {
            links[i].SetActive(i < numLinks);
        }

        // Instantiate required links
        int currentLinkCount = links.Count;
        if (currentLinkCount < numLinks)
        {
            int linksToAdd = numLinks - currentLinkCount;
            for (int i = 0; i < linksToAdd; i++)
            {
                GameObject linkObject = Instantiate(linkPrefab, linksContainer.transform);
                linkObject.name = $"Link_{currentLinkCount + i + 1}";
                links.Add(linkObject);
            }
        }

        Vector2[] linkMeshVectors = GeometryUtils.CenteredRectangle(linkWidth, linkHeight);

        // Connect each link to each other, and start with the hook
        SceneRigidBody previousLinkRB = hook;
        for (int i = 0; i < links.Count; i++)
        {
            GameObject linkObject = links[i];

            if (linkObject.TryGetComponent<SceneRigidBody>(out var linkRB))
            {
                linkRB.OverridePolygonPoints(linkMeshVectors);

                linkRB.rbInput.localLinkPosOtherRB = new Vector2(0, -(linkHeight + inBetweenLength) / 2.0f);
                linkRB.rbInput.localLinkPosThisRB = new Vector2(0, (linkHeight + inBetweenLength) / 2.0f);

                linkRB.rbInput.colliderType = linkColliderType;
                linkRB.rbInput.mass = Mathf.Lerp(firstLinkMass, lastLinkMass, i / (float)(links.Count - 0.99f));

                linkRB.rbInput.linkedRigidBody = (i == 0) ? hook : previousLinkRB;
                linkRB.rbInput.isInteractable = mouseInteraction == MouseInteraction.AllLinks || (mouseInteraction == MouseInteraction.OnlyLastLink && (i == links.Count - 1));

                previousLinkRB = linkRB;
            }
        }
    }
}