using Mono.Cecil;
using System;
using System.Collections.Generic;
using UnityEngine.AI;
using UnityEngine;


namespace PathFinding.BehaviourTrees
{
    public interface IStrategy
    {
        Node.Status Process();

        void Reset()
        {
            // Noop
        }
    }


    public class ActionStrategy : IStrategy
    {
        readonly Action doSomething;

        public ActionStrategy(Action doSomething)
        {
            this.doSomething = doSomething;
        }

        public Node.Status Process()
        {
            doSomething();
            return Node.Status.Success;
        }
    }

    public class Condition : IStrategy
    {
        readonly Func<bool> predicate;

        public Condition(Func<bool> predicate)
        {
            this.predicate = predicate;
        }

        public Node.Status Process() => predicate() ? Node.Status.Success : Node.Status.Failure;
    }

    public class PatrolStrategy : IStrategy
    {
        readonly Transform entity;
        readonly NavMeshAgent agent;
        readonly List<Transform> patrolPoints;
        readonly float patrolSpeed;
        bool isMovingToPoint;
        int currentIndex;
        


        public PatrolStrategy(Transform entity, NavMeshAgent agent, List<Transform> patrolPoints, float patrolSpeed = 2f)
        {
            this.entity = entity;
            this.agent = agent;
            this.patrolPoints = patrolPoints;
            this.patrolSpeed = patrolSpeed;
            this.currentIndex = 0;
            this.isMovingToPoint = false;
        }

        public Node.Status Process()
        {

            //Debug.Log(agent.remainingDistance);
            //Debug.Log($"agent.pathPending: {agent.pathPending}");

            if (currentIndex == patrolPoints.Count) return Node.Status.Success;

            var target = patrolPoints[currentIndex];

            if (!isMovingToPoint)
            {
                agent.SetDestination(target.position);
                isMovingToPoint = true;
                return Node.Status.Running;
            }
            
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance
                && (!agent.hasPath || agent.velocity.sqrMagnitude == 0f))
            {
                currentIndex++;
                if(currentIndex >= patrolPoints.Count)
                    return Node.Status.Success;

                target = patrolPoints[currentIndex];
                agent.SetDestination(target.position);
                Debug.Log($"nextIndex: {currentIndex} ");
            }
            else if (!agent.hasPath)
            {
                agent.SetDestination(target.position);
            }

            return Node.Status.Running;
        }

        public void Reset() => currentIndex = 0;
    }

    public class MoveToTarget : IStrategy {
        readonly Transform entity;
        readonly NavMeshAgent agent;
        readonly Transform target;
        bool isPathCalculated;

        public MoveToTarget(Transform entity, NavMeshAgent agent, Transform target) {
            this.entity = entity;
            this.agent = agent;
            this.target = target;
        }

        public Node.Status Process() {
            if (Vector3.Distance(entity.position, target.position) < 1f) {
                return Node.Status.Success;
            }
            
            agent.SetDestination(target.position);
            entity.LookAt(target.position);

            if (agent.pathPending) {
                isPathCalculated = true;
            }
            Debug.Log("Running...");
            return Node.Status.Running;
        }

        public void Reset() => isPathCalculated = false;
    }    

}