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

    //  Run in OnUpdate(), only schedule more jobs if true
    public bool AllJobsCompleted()
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

    //  Schedule jobs using this method
    public void ScheduleNewJob<T>(T job) where T : struct, IJob
    {
        JobHandle newHandle = IJobExtensions.Schedule<T>(job, previousDependency);

        allJobDependencies = JobHandle.CombineDependencies(newHandle, allJobDependencies);
        previousDependency = newHandle;
    }

    //  Call in OnDestroy()
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
