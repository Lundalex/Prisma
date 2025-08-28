using Resources2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using PM = ProgramManager;

public class ShaderHelper : MonoBehaviour
{
    public Main m;
    public AssetReferenceT<Texture2DArray> addressableCausticsTexture;
    public void SetPSimShaderBuffers(ComputeShader pSimShader)
    {
        // Kernel PreCalculations
        pSimShader.SetBuffer(0, "PDatas", m.PDataBuffer);
        pSimShader.SetBuffer(0, "PTypes", m.PTypeBuffer);

        // Kernel PreCalculations
        pSimShader.SetBuffer(1, "SpatialLookup", m.SpatialLookupBuffer);
        pSimShader.SetBuffer(1, "StartIndices", m.StartIndicesBuffer);

        pSimShader.SetBuffer(1, "PDatas", m.PDataBuffer);
        pSimShader.SetBuffer(1, "PTypes", m.PTypeBuffer);

        pSimShader.SetBuffer(2, "ParticleSpringsCombined", m.ParticleSpringsCombinedBuffer);

        // Kernel TransferAllSpringData - 8/8 buffers
        pSimShader.SetBuffer(3, "PDatas", m.PDataBuffer);
        pSimShader.SetBuffer(3, "PTypes", m.PTypeBuffer);
        pSimShader.SetBuffer(3, "SpatialLookup", m.SpatialLookupBuffer);
        pSimShader.SetBuffer(3, "StartIndices", m.StartIndicesBuffer);
        pSimShader.SetBuffer(3, "SpringCapacities", m.SpringCapacitiesBuffer);
        pSimShader.SetBuffer(3, "SpringStartIndices_dbA", m.SpringStartIndicesBuffer_dbA);
        pSimShader.SetBuffer(3, "SpringStartIndices_dbB", m.SpringStartIndicesBuffer_dbB);
        pSimShader.SetBuffer(3, "ParticleSpringsCombined", m.ParticleSpringsCombinedBuffer);

        // Kernel ParticleForces - 8/8 buffers
        pSimShader.SetBuffer(4, "SpatialLookup", m.SpatialLookupBuffer);
        pSimShader.SetBuffer(4, "StartIndices", m.StartIndicesBuffer);

        pSimShader.SetBuffer(4, "PDatas", m.PDataBuffer);
        pSimShader.SetBuffer(4, "PTypes", m.PTypeBuffer);

        pSimShader.SetBuffer(4, "SpringCapacities", m.SpringCapacitiesBuffer);
        pSimShader.SetBuffer(4, "SpringStartIndices_dbA", m.SpringStartIndicesBuffer_dbA);
        pSimShader.SetBuffer(4, "SpringStartIndices_dbB", m.SpringStartIndicesBuffer_dbB);
        pSimShader.SetBuffer(4, "ParticleSpringsCombined", m.ParticleSpringsCombinedBuffer);

        pSimShader.SetBuffer(5, "PDatas", m.PDataBuffer);
        pSimShader.SetBuffer(5, "PTypes", m.PTypeBuffer);
        pSimShader.SetBuffer(5, "SpringCapacities", m.SpringCapacitiesBuffer);

        pSimShader.SetBuffer(6, "RecordedFluidDatas", m.RecordedFluidDataBuffer);

        pSimShader.SetBuffer(7, "SpatialLookup", m.SpatialLookupBuffer);
        pSimShader.SetBuffer(7, "PDatas", m.PDataBuffer);
        pSimShader.SetBuffer(7, "PTypes", m.PTypeBuffer);
        pSimShader.SetBuffer(7, "RecordedFluidDatas", m.RecordedFluidDataBuffer);
    }

    public void SetRenderShaderBuffers(ComputeShader renderShader)
    {
        renderShader.SetBuffer(0, "ShadowMask", m.ShadowMask_dbA);

        if (m.ParticlesNum != 0)
        {
            renderShader.SetBuffer(1, "SpatialLookup", m.SpatialLookupBuffer);
            renderShader.SetBuffer(1, "StartIndices", m.StartIndicesBuffer);
            renderShader.SetBuffer(1, "PDatas", m.PDataBuffer);
            renderShader.SetBuffer(1, "PTypes", m.PTypeBuffer);
            renderShader.SetBuffer(1, "Materials", m.MaterialBuffer);
            renderShader.SetBuffer(1, "SensorAreas", m.SensorAreaBuffer);
            renderShader.SetBuffer(1, "ShadowMask", m.ShadowMask_dbA);
        }

        if (m.NumRigidBodies != 0)
        {
            renderShader.SetBuffer(2, "RigidBodies", m.RBDataBuffer);
            renderShader.SetBuffer(2, "RBVectors", m.RBVectorBuffer);
            renderShader.SetBuffer(2, "Materials", m.MaterialBuffer);
            renderShader.SetBuffer(2, "ShadowMask", m.ShadowMask_dbA);

            renderShader.SetBuffer(3, "RigidBodies", m.RBDataBuffer);
            renderShader.SetBuffer(3, "Materials", m.MaterialBuffer);
            renderShader.SetBuffer(3, "ShadowMask", m.ShadowMask_dbA);
        }
    }

    public void SetRenderShaderTextures(ComputeShader renderShader)
    {
        renderShader.SetTexture(0, "Result", m.renderTexture);
        renderShader.SetTexture(0, "Background", m.backgroundTexture);

        renderShader.SetTexture(1, "Result", m.renderTexture);
        renderShader.SetTexture(1, "DynamicCaustics", m.dynamicCausticsTexture);
        if (m.precomputedCausticsTexture != null) renderShader.SetTexture(1, "PrecomputedCaustics", m.precomputedCausticsTexture);
        renderShader.SetTexture(1, "LiquidVelocityGradient", m.LiquidVelocityGradientTexture);
        renderShader.SetTexture(1, "GasVelocityGradient", m.GasVelocityGradientTexture);
        renderShader.SetTexture(1, "Background", m.backgroundTexture);
        renderShader.SetTexture(1, "Atlas", m.AtlasTexture);

        renderShader.SetTexture(2, "Result", m.renderTexture);
        renderShader.SetTexture(2, "Background", m.backgroundTexture);
        renderShader.SetTexture(2, "Atlas", m.AtlasTexture);

        renderShader.SetTexture(3, "Result", m.renderTexture);
        renderShader.SetTexture(3, "Atlas", m.AtlasTexture);

        renderShader.SetTexture(4, "Result", m.renderTexture);
        renderShader.SetTexture(4, "UITexture", m.uiTexture);
    }

    public void SetPostProcessorBuffers(ComputeShader ppShader)
    {
        ppShader.SetBuffer(0, "ShadowMask_dbA", m.ShadowMask_dbA);
        ppShader.SetBuffer(0, "ShadowMask_dbB", m.ShadowMask_dbB);
        ppShader.SetBuffer(0, "SharpShadowMask", m.SharpShadowMask);
        ppShader.SetBuffer(0, "ShadowDstMask", m.ShadowDstMask);
        ppShader.SetBuffer(0, "RimLightMask", m.RimLightMask);

        ppShader.SetBuffer(1, "ShadowMask_dbA", m.ShadowMask_dbA);
        ppShader.SetBuffer(1, "ShadowMask_dbB", m.ShadowMask_dbB);
        ppShader.SetBuffer(1, "SharpShadowMask", m.SharpShadowMask);
        ppShader.SetBuffer(1, "ShadowDstMask", m.ShadowDstMask);
        ppShader.SetBuffer(1, "RimLightMask", m.RimLightMask);

        ppShader.SetBuffer(2, "ShadowMask_dbA", m.ShadowMask_dbA);
        ppShader.SetBuffer(2, "ShadowMask_dbB", m.ShadowMask_dbB);
        ppShader.SetBuffer(2, "SharpShadowMask", m.SharpShadowMask);
        ppShader.SetBuffer(2, "ShadowDstMask", m.ShadowDstMask);
        ppShader.SetBuffer(2, "RimLightMask", m.RimLightMask);

        ppShader.SetBuffer(3, "ShadowMask_dbA", m.ShadowMask_dbA);
        ppShader.SetBuffer(3, "SharpShadowMask", m.SharpShadowMask);

        ppShader.SetBuffer(4, "ShadowMask_dbA", m.ShadowMask_dbA);
        ppShader.SetBuffer(4, "ShadowMask_dbB", m.ShadowMask_dbB);
        ppShader.SetBuffer(4, "ShadowDstMask", m.ShadowDstMask);

        ppShader.SetBuffer(5, "ShadowMask_dbA", m.ShadowMask_dbA);
        ppShader.SetBuffer(5, "ShadowMask_dbB", m.ShadowMask_dbB);
        ppShader.SetBuffer(5, "ShadowDstMask", m.ShadowDstMask);

        ppShader.SetBuffer(6, "SharpShadowMask", m.SharpShadowMask);
        ppShader.SetBuffer(6, "RimLightMask", m.RimLightMask);

        ppShader.SetBuffer(7, "ShadowMask_dbA", m.ShadowMask_dbA);
        ppShader.SetBuffer(7, "ShadowMask_dbB", m.ShadowMask_dbB);
        ppShader.SetBuffer(7, "SharpShadowMask", m.SharpShadowMask);
        ppShader.SetBuffer(7, "RimLightMask", m.RimLightMask);
    }

    public void SetPostProcessorTextures(ComputeShader ppShader)
    {
        ppShader.SetTexture(6, "Result", m.renderTexture);
        ppShader.SetTexture(6, "PPResult", m.ppRenderTexture);

        ppShader.SetTexture(7, "Result", m.renderTexture);
        ppShader.SetTexture(7, "PPResult", m.ppRenderTexture);

        ppShader.SetTexture(8, "Result", m.renderTexture);
        ppShader.SetTexture(8, "PPResult", m.ppRenderTexture);
    }

    public void SetSortShaderBuffers(ComputeShader sortShader)
    {
        sortShader.SetBuffer(0, "SpatialLookup", m.SpatialLookupBuffer);

        sortShader.SetBuffer(0, "PDatas", m.PDataBuffer);
        sortShader.SetBuffer(0, "PTypes", m.PTypeBuffer);

        sortShader.SetBuffer(1, "SpatialLookup", m.SpatialLookupBuffer);

        sortShader.SetBuffer(1, "PDatas", m.PDataBuffer);
        sortShader.SetBuffer(1, "PTypes", m.PTypeBuffer);

        sortShader.SetBuffer(2, "StartIndices", m.StartIndicesBuffer);

        sortShader.SetBuffer(3, "SpatialLookup", m.SpatialLookupBuffer);
        sortShader.SetBuffer(3, "StartIndices", m.StartIndicesBuffer);
        sortShader.SetBuffer(3, "PTypes", m.PTypeBuffer);
        sortShader.SetBuffer(3, "PDatas", m.PDataBuffer);

        sortShader.SetBuffer(4, "SpatialLookup", m.SpatialLookupBuffer);
        sortShader.SetBuffer(4, "StartIndices", m.StartIndicesBuffer);
        sortShader.SetBuffer(4, "SpringCapacities", m.SpringCapacitiesBuffer);

        sortShader.SetBuffer(5, "SpringCapacities", m.SpringCapacitiesBuffer);

        sortShader.SetBuffer(6, "SpringCapacities", m.SpringCapacitiesBuffer);
        sortShader.SetBuffer(6, "SpringStartIndices_dbA", m.SpringStartIndicesBuffer_dbA);
        sortShader.SetBuffer(6, "SpringStartIndices_dbB", m.SpringStartIndicesBuffer_dbB);
        sortShader.SetBuffer(6, "SpringStartIndices_dbC", m.SpringStartIndicesBuffer_dbC);

        sortShader.SetBuffer(7, "SpringStartIndices_dbA", m.SpringStartIndicesBuffer_dbA);
        sortShader.SetBuffer(7, "SpringStartIndices_dbB", m.SpringStartIndicesBuffer_dbB);
        sortShader.SetBuffer(7, "SpringStartIndices_dbC", m.SpringStartIndicesBuffer_dbC);

        sortShader.SetBuffer(8, "SpringStartIndices_dbA", m.SpringStartIndicesBuffer_dbA);
        sortShader.SetBuffer(8, "SpringStartIndices_dbB", m.SpringStartIndicesBuffer_dbB);
        sortShader.SetBuffer(8, "SpringStartIndices_dbC", m.SpringStartIndicesBuffer_dbC);
    }

    public void UpdatePSimShaderVariables(ComputeShader pSimShader)
    {
        pSimShader.SetInt("MaxInfluenceRadiusSqr", m.MaxInfluenceRadiusSqr);
        pSimShader.SetFloat("InvMaxInfluenceRadius", m.InvMaxInfluenceRadius);
        pSimShader.SetVector("ChunksNum", Utils.Int2ToVector2(m.ChunksNum));
        pSimShader.SetInt("ChunksNumAll", m.ChunksNumAll);
        pSimShader.SetVector("BoundaryDims", Utils.Int2ToVector2(m.BoundaryDims));
        pSimShader.SetInt("ParticlesNum", m.ParticlesNum);
        pSimShader.SetInt("PTypesNum", m.PTypesNum);
        pSimShader.SetInt("ParticleSpringsCombinedHalfLength", m.ParticleSpringsCombinedHalfLength);
        pSimShader.SetInt("MaxInfluenceRadius", m.MaxInfluenceRadius);
        pSimShader.SetInt("SubTimeStepsPerFrame", m.SubTimeStepsPerFrame);
        pSimShader.SetFloat("LookAheadTime", m.LookAheadTime);
        pSimShader.SetFloat("StateThresholdPadding", m.StateThresholdPadding);
        pSimShader.SetFloat("FluidPadding", Mathf.Max(m.FluidPadding, 0.1f));
        pSimShader.SetFloat("MaxInteractionRadius", m.MaxInteractionRadius);
        pSimShader.SetFloat("MaxPVel", m.MaxPVel);
        pSimShader.SetFloat("InteractionAttractionPower", m.InteractionAttractionPower);
        pSimShader.SetFloat("InteractionRepulsionPower", m.InteractionRepulsionPower);
        pSimShader.SetFloat("InteractionFountainPower", m.InteractionFountainPower);
        pSimShader.SetFloat("InteractionTemperaturePower", m.InteractionTemperaturePower);
        pSimShader.SetFloat("InteractionDampening", m.InteractionDampening);
    }

    public void UpdateRenderShaderVariables(ComputeShader renderShader)
    {
        renderShader.SetFloat("LiquidMetaballsThreshold", m.LiquidMetaballsThreshold);
        renderShader.SetFloat("LiquidMetaballsEdgeDensityWidth", m.LiquidMetaballsEdgeDensityWidth);
        renderShader.SetFloat("VisualLiquidParticleRadius", m.VisualLiquidParticleRadius);
        renderShader.SetFloat("LiquidEdgeWidth", m.LiquidEdgeWidth);
        renderShader.SetFloat("InvLiquidVelocityGradientMaxValue", 1 / m.LiquidVelocityGradientMaxValue);

        renderShader.SetFloat("GasMetaballsThreshold", m.GasMetaballsThreshold);
        renderShader.SetFloat("GasMetaballsEdgeDensityWidth", m.GasMetaballsEdgeDensityWidth);
        renderShader.SetFloat("VisualGasParticleRadius", m.VisualGasParticleRadius);
        renderShader.SetFloat("GasEdgeWidth", m.GasEdgeWidth);
        renderShader.SetFloat("InvGasVelocityGradientMaxValue", 1 / m.GasVelocityGradientMaxValue);
        renderShader.SetFloat("GasNoiseStrength", m.GasNoiseStrength);
        renderShader.SetFloat("GasNoiseDensityDarkeningFactor", m.GasNoiseDensityDarkeningFactor);
        renderShader.SetFloat("GasNoiseDensityOpacityFactor", m.GasNoiseDensityOpacityFactor);

        renderShader.SetFloat("BackgroundUpScaleFactor", m.BackgroundUpScaleFactor);
        renderShader.SetVector("BackgroundBrightness", Utils.Float3ToVector3(m.BackgroundBrightness));
        renderShader.SetBool("MirrorRepeatBackgroundUV", m.MirrorRepeatBackgroundUV);

        renderShader.SetFloat("RBEdgeWidth", m.RBEdgeWidth);
        renderShader.SetFloat("FluidSensorEdgeWidth", m.FluidSensorEdgeWidth);
        renderShader.SetFloat("SensorAreaAnimationSpeed", m.SensorAreaAnimationSpeed);

        renderShader.SetFloat("RBShadowStrength", m.RBShadowStrength);
        renderShader.SetFloat("LiquidShadowStrength", m.LiquidShadowStrength);
        renderShader.SetFloat("GasShadowStrength", m.GasShadowStrength);

        renderShader.SetInt("SpringRenderNumPeriods", m.SpringRenderNumPeriods);
        renderShader.SetFloat("SpringRenderWidth", m.SpringRenderWidth);
        renderShader.SetFloat("SpringRenderHalfMatWidth", m.SpringRenderMatWidth / 2.0f);
        renderShader.SetFloat("SpringRenderRodLength", Mathf.Max(m.SpringRenderRodLength, 0.01f));
        renderShader.SetFloat("TaperThresoldNormalised", m.TaperThresoldNormalised);
        renderShader.SetVector("SpringTextureUVFactor", Utils.Float2ToVector2(m.SpringTextureUVFactor));

        renderShader.SetVector("Resolution", PM.Instance.Resolution);
        renderShader.SetVector("BoundaryDims", Utils.Int2ToVector2(m.BoundaryDims));
        renderShader.SetVector("ViewScale", PM.Instance.ViewScale);
        renderShader.SetVector("ViewOffset", PM.Instance.ViewOffset);

        renderShader.SetInt("MaxInfluenceRadius", m.MaxInfluenceRadius);
        renderShader.SetFloat("InvMaxInfluenceRadius", m.InvMaxInfluenceRadius);
        renderShader.SetInt("MaxInfluenceRadiusSqr", m.MaxInfluenceRadiusSqr);
        renderShader.SetVector("ChunksNum", Utils.Int2ToVector2(m.ChunksNum));
        renderShader.SetInt("ChunksNumAll", m.ChunksNumAll);
        renderShader.SetInt("ParticlesNum", m.ParticlesNum);
        renderShader.SetInt("PTypesNum", m.PTypesNum);
        renderShader.SetInt("NumRigidBodies", m.NumRigidBodies);
        renderShader.SetInt("NumFluidSensors", m.NumFluidSensors);
        renderShader.SetVector("PrecomputedCausticsDims", Utils.Int3ToVector3(m.PrecomputedCausticsDims));
        renderShader.SetFloat("PrecomputedCausticsScaleFactor", m.PrecomputedCausticsScaleFactor);
        renderShader.SetFloat("DynamicCausticsScaleFactor", m.DynamicCausticsScaleFactor);
        renderShader.SetFloat("PrecomputedCausticsZBlurFactor", m.PrecomputedCausticsZBlurFactor);

        renderShader.SetVector("GlobalBrightness", Utils.Float3ToVector3(m.GlobalBrightness));
        renderShader.SetFloat("Contrast", m.Contrast);
        renderShader.SetFloat("Saturation", m.Saturation);
        renderShader.SetFloat("Gamma", m.Gamma);
    }

    public void SetPostProcessorVariables(ComputeShader ppShader)
    {
        ppShader.SetFloat("ShadowDarkness", m.ShadowDarkness);
        ppShader.SetFloat("ShadowFalloff", m.ShadowFalloff);

        ppShader.SetVector("Resolution", PM.Instance.Resolution);
        ppShader.SetVector("ShadowDirection", new(-Mathf.Cos(Mathf.Deg2Rad * m.ShadowDirection), -Mathf.Sin(Mathf.Deg2Rad * m.ShadowDirection)));

        ppShader.SetInt("ShadowBlurRadius", Mathf.Max(0, m.ShadowBlurRadius));
        ppShader.SetFloat("ShadowDiffusion", Mathf.Max(0f, m.ShadowDiffusion));

        ppShader.SetInt("CastedShadowType", (int)m.CastedShadowType);
        ppShader.SetFloat("RimShadingStrength", m.RimShadingStrength);
        ppShader.SetFloat("RimShadingBleed", m.RimShadingBleed);
        ppShader.SetFloat("RimShadingOpaqueBleed", m.RimShadingOpaqueBleed);
    }

    public void UpdateSortShaderVariables(ComputeShader sortShader)
    {
        sortShader.SetInt("MaxInfluenceRadius", m.MaxInfluenceRadius);
        sortShader.SetVector("ChunksNum", Utils.Int2ToVector2(m.ChunksNum));
        sortShader.SetInt("ChunksNumAll", m.ChunksNumAll);
        sortShader.SetInt("ParticlesNum", m.ParticlesNum);
        sortShader.SetInt("ParticlesNum_NextPow2", m.ParticlesNum_NextPow2);
    }

    // --- Ner RB shader ---

    public void SetRBSimShaderBuffers(ComputeShader rbSimShader)
    {
        rbSimShader.SetBuffer(0, "RigidBodies", m.RBDataBuffer);
        rbSimShader.SetBuffer(0, "RBVectors", m.RBVectorBuffer);
        rbSimShader.SetBuffer(0, "RecordedFluidDatas", m.RecordedFluidDataBuffer);

        rbSimShader.SetBuffer(0, "SpatialLookup", m.SpatialLookupBuffer);
        rbSimShader.SetBuffer(0, "PTypes", m.PTypeBuffer);
        rbSimShader.SetBuffer(0, "PDatas", m.PDataBuffer);
        rbSimShader.SetBuffer(0, "RBAdjustments", m.RBAdjustmentBuffer);

        rbSimShader.SetBuffer(1, "RigidBodies", m.RBDataBuffer);
        rbSimShader.SetBuffer(1, "RBVectors", m.RBVectorBuffer);
        rbSimShader.SetBuffer(1, "RBAdjustments", m.RBAdjustmentBuffer);

        rbSimShader.SetBuffer(2, "RigidBodies", m.RBDataBuffer);
        rbSimShader.SetBuffer(2, "RBAdjustments", m.RBAdjustmentBuffer);

        rbSimShader.SetBuffer(3, "RigidBodies", m.RBDataBuffer);
        rbSimShader.SetBuffer(3, "RBAdjustments", m.RBAdjustmentBuffer);

        rbSimShader.SetBuffer(4, "RigidBodies", m.RBDataBuffer);
        rbSimShader.SetBuffer(4, "RBVectors", m.RBVectorBuffer);
        
        rbSimShader.SetBuffer(5, "RigidBodies", m.RBDataBuffer);
        rbSimShader.SetBuffer(5, "RBVectors", m.RBVectorBuffer);
        rbSimShader.SetBuffer(5, "RBAdjustments", m.RBAdjustmentBuffer);
        rbSimShader.SetBuffer(5, "RecordedFluidDatas", m.RecordedFluidDataBuffer);

        rbSimShader.SetBuffer(6, "RigidBodies", m.RBDataBuffer);
        rbSimShader.SetBuffer(6, "RBVectors", m.RBVectorBuffer);
    }

    public void UpdateRBSimShaderVariables(ComputeShader rbSimShader)
    {
        rbSimShader.SetVector("BoundaryDims", Utils.Int2ToVector2(m.BoundaryDims));
        rbSimShader.SetFloat("RigidBodyPadding", m.RigidBodyPadding);
        rbSimShader.SetFloat("BoundaryElasticity", m.BoundaryElasticity);
        rbSimShader.SetFloat("BoundaryFriction", m.BoundaryFriction);

        rbSimShader.SetInt("NumRigidBodies", m.NumRigidBodies);
        rbSimShader.SetInt("NumVectors", m.NumRigidBodyVectors);
        rbSimShader.SetInt("NumParticles", m.ParticlesNum);
        rbSimShader.SetVector("ChunksNum", Utils.Int2ToVector2(m.ChunksNum));
        rbSimShader.SetInt("ChunksNumAll", m.ChunksNumAll);
        rbSimShader.SetInt("PTypesNum", m.PTypesNum);

        rbSimShader.SetBool("AllowLinkedRBCollisions", m.AllowLinkedRBCollisions);
        rbSimShader.SetFloat("RB_RBCollisionCorrectionFactor", m.RB_RBCollisionCorrectionFactor);
        rbSimShader.SetFloat("RB_RBFixedCollisionCorrection", m.RB_RBFixedCollisionCorrection);
        rbSimShader.SetFloat("RB_RBRigidConstraintCorrectionFactor", m.RB_RBRigidConstraintCorrectionFactor);

        rbSimShader.SetFloat("MaxRBRotVel", m.MaxRBRotVel);
        rbSimShader.SetFloat("MaxRBVel", m.MaxRBVel);
        rbSimShader.SetFloat("MinRBVelForMovement", m.MinRBVelForMovement);

        rbSimShader.SetFloat("RB_MaxInteractionRadius", m.RB_MaxInteractionRadius);
        rbSimShader.SetFloat("RB_InteractionAttractionPower", m.RB_InteractionAttractionPower);
        rbSimShader.SetFloat("RB_InteractionRepulsionPower", m.RB_InteractionRepulsionPower);
        rbSimShader.SetFloat("RB_InteractionDampening", m.RB_InteractionDampening);
    }
}