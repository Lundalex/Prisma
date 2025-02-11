using UnityEngine;

[ExecuteInEditMode]
public class SplineCurveSurface : Assembly
{
    [Header("References")]
    public SplineCurveDrawer splineCurveDrawer;
    public SceneRigidBody sceneRigidBody;

    public override void AssemblyUpdate()
    {
        if (splineCurveDrawer == null || sceneRigidBody == null)
        {
            Debug.LogWarning("SplineMeshAssembly: Missing references!");
            return;
        }
        
        Vector3[] splinePoints = splineCurveDrawer.CreateBoxSplinePoints(true, false);
        if (splinePoints == null || splinePoints.Length < 2)
        {
            Debug.LogWarning("SplineMeshAssembly: Not enough spline points!");
            return;
        }
        
        Vector2[] splinePoints2D = new Vector2[splinePoints.Length];
        for (int i = 0; i < splinePoints.Length; i++)
        {
            splinePoints2D[i] = new Vector2(splinePoints[i].x, splinePoints[i].y);
        }
        
        sceneRigidBody.OverridePolygonPoints(splinePoints2D);
    }
}