using Resources2;
using UnityEngine;
using PM = ProgramManager;

public class ShaderHelper : MonoBehaviour
{
    public Main m;
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
        if (m.ParticlesNum != 0)
        {
            renderShader.SetBuffer(1, "SpatialLookup", m.SpatialLookupBuffer);
            renderShader.SetBuffer(1, "StartIndices", m.StartIndicesBuffer);
            renderShader.SetBuffer(1, "PDatas", m.PDataBuffer);
            renderShader.SetBuffer(1, "PTypes", m.PTypeBuffer);
            renderShader.SetBuffer(1, "Materials", m.MaterialBuffer);
            renderShader.SetBuffer(1, "SensorAreas", m.SensorAreaBuffer);
        }

        if (m.NumRigidBodies != 0)
        {
            renderShader.SetBuffer(2, "RigidBodies", m.RBDataBuffer);
            renderShader.SetBuffer(2, "RBVectors", m.RBVectorBuffer);
            renderShader.SetBuffer(2, "Materials", m.MaterialBuffer);

            renderShader.SetBuffer(3, "RigidBodies", m.RBDataBuffer);
            renderShader.SetBuffer(3, "Materials", m.MaterialBuffer);
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

        renderShader.SetInt("SpringRenderNumPeriods", m.SpringRenderNumPeriods);
        renderShader.SetFloat("SpringRenderWidth", m.SpringRenderWidth);
        renderShader.SetFloat("SpringRenderHalfMatWidth", m.SpringRenderMatWidth / 2.0f);
        renderShader.SetFloat("SpringRenderRodLength", Mathf.Max(m.SpringRenderRodLength, 0.01f));
        renderShader.SetFloat("TaperThresoldNormalised", m.TaperThresoldNormalised);
        renderShader.SetVector("SpringTextureUVFactor", Utils.Float2ToVector2(m.SpringTextureUVFactor));

        renderShader.SetVector("Resolution", PM.Instance.Resolution);
        renderShader.SetVector("BoundaryDims", Utils.Int2ToVector2(m.BoundaryDims));
        renderShader.SetVector("ScreenToViewFactor", PM.Instance.ScreenToViewFactor);
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