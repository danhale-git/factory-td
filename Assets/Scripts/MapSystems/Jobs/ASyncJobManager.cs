using UnityEngine;
using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;

public struct ASyncJobManager
{
    EntityManager entityManager;
    public EntityCommandBuffer commandBuffer;
    public JobHandle previousDependency;
    JobHandle allJobDependencies;

    public bool NoJobsRunning()
    {
        if(commandBuffer.IsCreated)
        {
            if(!allJobDependencies.IsCompleted) return false;
            else
            {
                JobCompleteAndBufferPlayback();
                Initialise();
                return true;
            }
        }
        else
        {
            Initialise();
            return true;
        }
    }

    public void NewJobScheduled(JobHandle newHandle)
    {
        allJobDependencies = JobHandle.CombineDependencies(newHandle, allJobDependencies);
        previousDependency = newHandle;
    }

    public void Dispose()
    {
        if(commandBuffer.IsCreated) commandBuffer.Dispose();
    }

    void JobCompleteAndBufferPlayback()
	{
		allJobDependencies.Complete();
		commandBuffer.Playback(entityManager);
		commandBuffer.Dispose();
	}

    void Initialise()
    {
        entityManager = World.Active.EntityManager;
        commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
        allJobDependencies = new JobHandle();
		previousDependency = new JobHandle();
    }
}
