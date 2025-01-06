using Resources2;
using UnityEngine;

public class SeesawScale : Assembly
{
    [Header("Lever Arm")]
    [Range(0.1f, 0.9f)] public float leverArmJointLerpFactor = 0.5f;

    [Header("References")]
    [SerializeField] private SceneRigidBody leftBucket;
    [SerializeField] private SceneRigidBody rightBucket;
    [SerializeField] private SceneRigidBody plank;
    [SerializeField] private Transform rotationJoint;
    [SerializeField] private UserSliderInput userSliderInput;

    // Private static
    private static float storedLeverArmJointLerpFactor;
    private static bool dataHasBeenStored = false;

    private void OnEnable()
    {
        ProgramManager.Instance.OnPreStart += AssemblyUpdate;
        RetrieveData();
    }

    private void OnDestroy()
    {
        StoreData();
        ProgramManager.Instance.OnPreStart -= AssemblyUpdate;
    }

    private void StoreData()
    {
        dataHasBeenStored = true;
        
        storedLeverArmJointLerpFactor = leverArmJointLerpFactor;
    }

    private void RetrieveData()
    {
        if (!dataHasBeenStored) return;

        leverArmJointLerpFactor = storedLeverArmJointLerpFactor;
    }

    public override void AssemblyUpdate()
    {
        if (leftBucket == null || rightBucket == null || plank == null || rotationJoint == null)
        {
            Debug.LogWarning("All references are not set. SeesawScale: " + this.name);
            return;
        }
        
        if (userSliderInput != null) userSliderInput.startValue = leverArmJointLerpFactor;

        Vector2 leftPlank = new(-75, 0);
        Vector2 rightPlank = new(75, 0);

        Vector2 lerpPos = Func.LerpVector2(leftPlank, rightPlank, leverArmJointLerpFactor);
        Vector2 bucketOffset = -lerpPos;

        Vector2 leftBucketPos = new Vector2(-100, 0) + bucketOffset;
        Vector2 rightBucketPos = new Vector2(100, 0) + bucketOffset;

        plank.rbInput.overrideCentroidPosition = lerpPos;
        rotationJoint.localPosition = lerpPos;
        leftBucket.rbInput.localLinkPosOtherRB = leftBucketPos;
        rightBucket.rbInput.localLinkPosOtherRB = rightBucketPos;
    }
}