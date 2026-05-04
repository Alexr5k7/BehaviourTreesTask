using PathFinding.BehaviourTrees;
using UnityEngine;
using UnityEngine.AI;

namespace PathFinding.FSM
{
    // ─────────────────────────────────────────────
    // Idle
    // ─────────────────────────────────────────────
    public class IdleState : IState
    {
        readonly RobberController robber;
        public IdleState(RobberController robber) { this.robber = robber; }

        public void OnEnter()
        {
            robber.Agent.ResetPath();
            Debug.Log($"[FSM] → Idle | HasOrder={robber.HasOrder} | HayObjetivos={robber.HayObjetivosDisponibles()}");
        }
        public void OnUpdate()
        {
            // Log cada 2s para confirmar que Idle está activo y esperando
            debugTimer += UnityEngine.Time.deltaTime;
            if (debugTimer > 2f)
            {
                debugTimer = 0f;
                Debug.Log($"[FSM] Idle tick | HasOrder={robber.HasOrder}");
            }
        }
        float debugTimer;
        public void OnExit() { debugTimer = 0f; }
    }

    // ─────────────────────────────────────────────
    // EnteringGallery
    // 
    // Controla el movimiento directamente con el
    // NavMeshAgent para evitar ambigüedades del BT.
    //
    // Fase 1: SetDestination(FrontDoor) → al llegar,
    //         desactivar puerta.
    // Fase 2: SetDestination(WP1) → al llegar,
    //         EnteredGallery = true.
    // ─────────────────────────────────────────────
    public class EnteringGalleryState : IState
    {
        readonly RobberController robber;

        enum Phase { GoToFrontDoor, GoInside }
        Phase phase;
        bool destinationSet;

        public EnteringGalleryState(RobberController robber) { this.robber = robber; }

        public void OnEnter()
        {
            phase = Phase.GoToFrontDoor;
            destinationSet = false;
            Debug.Log("[FSM] → EnteringGallery");
        }

        public void OnUpdate()
        {
            NavMeshAgent agent = robber.Agent;

            if (phase == Phase.GoToFrontDoor)
            {
                // Dar destino una sola vez
                if (!destinationSet)
                {
                    agent.SetDestination(robber.frontDoor.transform.position);
                    destinationSet = true;
                    return;
                }

                // Esperar a que la ruta esté calculada
                if (agent.pathPending) return;

                // Comprobar llegada
                if (agent.remainingDistance <= robber.arrivalThreshold)
                {
                    robber.DestroyFrontDoor();
                    phase = Phase.GoInside;
                    destinationSet = false;
                    Debug.Log("[FSM] EnteringGallery → fase 2 interior (WP1)");
                }
            }
            else // GoInside
            {
                if (!destinationSet)
                {
                    Transform wp1 = robber.waypoints.Count > 0
                        ? robber.waypoints[0]
                        : robber.transform;
                    agent.SetDestination(wp1.position);
                    destinationSet = true;
                    return;
                }

                if (agent.pathPending) return;

                if (agent.remainingDistance <= robber.arrivalThreshold)
                {
                    Debug.Log("[FSM] → Dentro de la galería.");
                    robber.SetEnteredGallery();
                }
            }
        }

        public void OnExit() { robber.ResetEnteredGallery(); }
    }

    // ─────────────────────────────────────────────
    // StealingTarget
    // Controlado directamente con NavMeshAgent.
    // Fases: MoveToTarget → Steal
    // ─────────────────────────────────────────────
    public class StealingTargetState : IState
    {
        readonly RobberController robber;
        NavMeshAgent agent => robber.Agent;

        enum Phase { Moving, Stealing }
        Phase phase;
        bool destinationSet;
        bool finished;

        public StealingTargetState(RobberController robber) { this.robber = robber; }

        public void OnEnter()
        {
            finished = false;
            destinationSet = false;
            phase = Phase.Moving;
            robber.SetStealthSpeed();
            Debug.Log($"[FSM] → StealingTarget: '{robber.CurrentTargetName}'");
        }

        public void OnUpdate()
        {
            if (finished) return;

            var target = robber.CurrentTarget;
            if (target == null) return;

            if (phase == Phase.Moving)
            {
                // Calcular distancia solo en XZ (objeto puede estar elevado)
                var ep = target.position;
                var pp = robber.transform.position;
                float distXZ = UnityEngine.Vector2.Distance(
                    new UnityEngine.Vector2(pp.x, pp.z),
                    new UnityEngine.Vector2(ep.x, ep.z));

                if (distXZ <= robber.stealReach)
                {
                    // Llegamos: robar
                    phase = Phase.Stealing;
                    agent.ResetPath();
                    target.gameObject.SetActive(false);
                    robber.RegistrarRobo();
                    Debug.Log($"[BT] ¡{robber.CurrentTargetName} robado!");
                    phase = Phase.Stealing;
                    return;
                }

                // Moverse hacia el punto en el suelo bajo el objetivo
                if (!destinationSet)
                {
                    agent.SetDestination(new UnityEngine.Vector3(ep.x, pp.y, ep.z));
                    destinationSet = true;
                }
                if (agent.pathPending) return;
                // Redirigir cada frame por si el path se perdió
                agent.SetDestination(new UnityEngine.Vector3(ep.x, pp.y, ep.z));
            }
            else // Stealing → listo
            {
                finished = true;
                Debug.Log("[FSM] → Objetivo robado, escapando.");
                robber.SetTargetStolen();
            }
        }

        public void OnExit() { finished = false; robber.ResetTargetStolen(); }
    }

    // ─────────────────────────────────────────────
    // Escaping
    // Controlado directamente con NavMeshAgent,
    // igual que EnteringGalleryState, sin BT.
    // Ruta: BackDoor → WP2 → WP3 → Van → WP4
    // ─────────────────────────────────────────────
    public class EscapingState : IState
    {
        readonly RobberController robber;
        NavMeshAgent agent => robber.Agent;

        // Puntos de la ruta de escape (se construyen en OnEnter)
        System.Collections.Generic.List<UnityEngine.Vector3> route;
        int routeIndex;
        int vanIndex;       // índice de la Van en la ruta
        bool destinationSet;
        bool finished;

        public EscapingState(RobberController robber) { this.robber = robber; }

        public void OnEnter()
        {
            robber.SetEscapeSpeed();
            finished = false;
            Debug.Log("[FSM] → Escaping");

            // Construir ruta: BackDoor, WP2, WP3, Van, WP4
            route = new System.Collections.Generic.List<UnityEngine.Vector3>();
            route.Add(robber.BackDoorPosition);

            var wps = robber.waypoints;
            if (wps.Count > 1 && wps[1] != null) route.Add(wps[1].position); // WP2
            if (wps.Count > 2 && wps[2] != null) route.Add(wps[2].position); // WP3
            if (robber.vanTransform != null) { vanIndex = route.Count; route.Add(robber.vanTransform.position); } // Van
            if (wps.Count > 3 && wps[3] != null) route.Add(wps[3].position); // WP4

            routeIndex = 0;
            destinationSet = false;
        }

        public void OnUpdate()
        {
            if (finished) return;

            if (routeIndex >= route.Count)
            {
                finished = true;
                robber.SetOutsideSpeed();
                Debug.Log("[FSM] → Esperando en WP4.");
                robber.SetEscaped();
                return;
            }

            // Destruir BackDoor al acercarse (primer punto de la ruta)
            if (routeIndex == 0 && robber.IsNearBackDoor())
                robber.DestroyBackDoor();

            // Dar destino una sola vez por punto
            if (!destinationSet)
            {
                agent.SetDestination(route[routeIndex]);
                destinationSet = true;
                return;
            }

            if (agent.pathPending) return;

            if (agent.remainingDistance <= robber.arrivalThreshold)
            {
                Debug.Log($"[Escape] Punto {routeIndex} alcanzado.");

                // Al llegar a la Van: comprobar victoria
                if (routeIndex == vanIndex)
                    robber.LlegarAVan();

                routeIndex++;
                destinationSet = false;
            }
        }

        public void OnExit() { robber.ResetEscaped(); }
    }

    // ─────────────────────────────────────────────
    // Done
    // ─────────────────────────────────────────────
    public class DoneState : IState
    {
        public void OnEnter() => Debug.Log("[FSM] → Done: ¡robo completado!");
        public void OnUpdate() { }
        public void OnExit() { }
    }
}