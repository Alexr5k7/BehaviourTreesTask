using System;
using System.Collections.Generic;
using UnityEngine.AI;
using UnityEngine;

namespace PathFinding.BehaviourTrees
{
    public interface IStrategy
    {
        Node.Status Process();
        void Reset() { /* Noop */ }
    }

    // ── ActionStrategy ───────────────────────────────────────
    public class ActionStrategy : IStrategy
    {
        readonly Action doSomething;
        public ActionStrategy(Action doSomething) { this.doSomething = doSomething; }

        public Node.Status Process()
        {
            doSomething();
            return Node.Status.Success;
        }
    }

    // ── Condition ────────────────────────────────────────────
    public class Condition : IStrategy
    {
        readonly Func<bool> predicate;
        public Condition(Func<bool> predicate) { this.predicate = predicate; }
        public Node.Status Process() => predicate() ? Node.Status.Success : Node.Status.Failure;
    }

    // ── PatrolStrategy  (recorre waypoints en orden) ─────────
    public class PatrolStrategy : IStrategy
    {
        readonly Transform entity;
        readonly NavMeshAgent agent;
        readonly List<Transform> patrolPoints;
        readonly float patrolSpeed;
        bool isMovingToPoint;
        int currentIndex;

        public PatrolStrategy(Transform entity, NavMeshAgent agent,
                              List<Transform> patrolPoints, float patrolSpeed = 2f)
        {
            this.entity = entity;
            this.agent = agent;
            this.patrolPoints = patrolPoints;
            this.patrolSpeed = patrolSpeed;
        }

        public Node.Status Process()
        {
            if (patrolPoints == null || patrolPoints.Count == 0) return Node.Status.Success;
            if (currentIndex >= patrolPoints.Count) return Node.Status.Success;

            var target = patrolPoints[currentIndex];

            if (!isMovingToPoint)
            {
                agent.SetDestination(target.position);
                isMovingToPoint = true;
                return Node.Status.Running;
            }

            bool arrived = !agent.pathPending
                        && agent.remainingDistance <= agent.stoppingDistance
                        && (!agent.hasPath || agent.velocity.sqrMagnitude == 0f);

            if (arrived)
            {
                currentIndex++;
                isMovingToPoint = false;
                if (currentIndex >= patrolPoints.Count) return Node.Status.Success;
                agent.SetDestination(patrolPoints[currentIndex].position);
            }
            else if (!agent.hasPath)
            {
                agent.SetDestination(target.position);
            }

            return Node.Status.Running;
        }

        public void Reset()
        {
            currentIndex = 0;
            isMovingToPoint = false;
        }
    }

    // ── MoveToTarget  (se mueve hacia un Transform) ──────────
    /// <summary>
    /// Mueve al agente hasta <target>. Retorna Success cuando la
    /// distancia es menor que <arrivalThreshold>.
    /// </summary>
    public class MoveToTarget : IStrategy
    {
        readonly Transform entity;
        readonly NavMeshAgent agent;
        readonly Transform target;
        readonly float arrivalThreshold;
        readonly bool horizontalOnly;   // ignora diferencia de altura

        public MoveToTarget(Transform entity, NavMeshAgent agent,
                            Transform target, float arrivalThreshold = 1f,
                            bool horizontalOnly = false)
        {
            this.entity = entity;
            this.agent = agent;
            this.target = target;
            this.arrivalThreshold = arrivalThreshold;
            this.horizontalOnly = horizontalOnly;
        }

        public Node.Status Process()
        {
            if (target == null) return Node.Status.Failure;

            float dist = horizontalOnly
                ? Vector2.Distance(
                    new Vector2(entity.position.x, entity.position.z),
                    new Vector2(target.position.x, target.position.z))
                : Vector3.Distance(entity.position, target.position);

            if (dist < arrivalThreshold) return Node.Status.Success;

            // SetDestination al punto en el suelo bajo el objetivo
            Vector3 destination = horizontalOnly
                ? new Vector3(target.position.x, entity.position.y, target.position.z)
                : target.position;

            agent.SetDestination(destination);
            entity.LookAt(new Vector3(target.position.x, entity.position.y, target.position.z));
            return Node.Status.Running;
        }

        public void Reset() => agent.ResetPath();
    }
    // ── MoveToPosition  (destino fijo en Vector3, no depende de Transform) ──
    /// Igual que MoveToTarget pero usa una posición fija.
    /// Útil cuando el GameObject destino puede desactivarse durante el trayecto.
    public class MoveToPosition : IStrategy
    {
        readonly Transform entity;
        readonly NavMeshAgent agent;
        readonly Vector3 destination;
        readonly float arrivalThreshold;

        public MoveToPosition(Transform entity, NavMeshAgent agent,
                              Vector3 destination, float arrivalThreshold = 1f)
        {
            this.entity = entity;
            this.agent = agent;
            this.destination = destination;
            this.arrivalThreshold = arrivalThreshold;
        }

        public Node.Status Process()
        {
            float dist = Vector3.Distance(
                new Vector3(entity.position.x, 0, entity.position.z),
                new Vector3(destination.x, 0, destination.z));

            if (dist < arrivalThreshold) return Node.Status.Success;

            agent.SetDestination(destination);
            return Node.Status.Running;
        }

        public void Reset() => agent.ResetPath();
    }
    // ── WaitStrategy  (espera N segundos, luego Success) ─────
    public class WaitStrategy : IStrategy
    {
        readonly float duration;
        readonly Action onStart;
        float elapsed;
        bool started;

        public WaitStrategy(float duration, Action onStart = null)
        {
            this.duration = duration;
            this.onStart = onStart;
        }

        public Node.Status Process()
        {
            if (!started)
            {
                onStart?.Invoke();
                started = true;
            }

            elapsed += Time.deltaTime;
            return elapsed >= duration ? Node.Status.Success : Node.Status.Running;
        }

        public void Reset()
        {
            elapsed = 0f;
            started = false;
        }
    }
}