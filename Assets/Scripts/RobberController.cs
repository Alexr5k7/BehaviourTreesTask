using System.Collections.Generic;
using PathFinding.BehaviourTrees;
using PathFinding.FSM;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NavMeshAgent))]
public class RobberController : MonoBehaviour
{
    [Header("Doors & Van")]
    [SerializeField] public GameObject frontDoor;
    [SerializeField] public GameObject backDoor;
    [SerializeField] public Transform vanTransform;

    [Header("Targets")]
    [SerializeField] public GameObject gem;
    [SerializeField] public List<GameObject> paintings;

    [Header("Waypoints (WP1 interior, WP2-WP4 escape)")]
    [SerializeField] public List<Transform> waypoints;

    [Header("Navigation")]
    [SerializeField] public float arrivalThreshold = 1.5f;
    [SerializeField] float outsideSpeed = 4f;  // velocidad en exterior (entrada y espera)
    [SerializeField] float stealthSpeed = 3f;
    [SerializeField] float escapeSpeed = 7f;
    [SerializeField] public float stealReach = 2.5f;
    [SerializeField] float angularSpeed = 180f;  // giro máximo (deg/s), evita patinar
    [SerializeField] float acceleration = 12f;   // aceleración/frenada

    // ── Público ──────────────────────────────────────────────
    public NavMeshAgent Agent { get; private set; }

    public BehaviourTree StealTree { get; private set; }

    public bool HasOrder { get; private set; }
    public bool EnteredGallery { get; private set; }
    public bool TargetStolen { get; private set; }
    public bool Escaped { get; private set; }

    // Setters explícitos — solo se activan desde los estados
    public void SetEnteredGallery() { EnteredGallery = true; }
    public void SetTargetStolen() { TargetStolen = true; }
    public void SetEscaped() { Escaped = true; }

    public string CurrentTargetName { get; private set; } = "?";
    public Transform CurrentTarget { get; private set; }

    // Objetos ya robados (para no permitir seleccionarlos de nuevo)
    readonly HashSet<GameObject> stolenObjects = new();
    public int stolenCount => stolenObjects.Count;

    // Referencia a la UI (se asigna automáticamente en Start)
    RobberUI ui;

    // Posición cacheada de la BackDoor (para usarla tras desactivarla)
    public Vector3 BackDoorPosition { get; private set; }

    // ── Privado ──────────────────────────────────────────────
    StateMachine fsm;

    // ── Unity ────────────────────────────────────────────────
    void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        Agent.angularSpeed = angularSpeed;
        Agent.acceleration = acceleration;
        Agent.updateRotation = false;  // rotación manual para evitar patinaje
    }

    void Start()
    {
        if (backDoor != null) BackDoorPosition = backDoor.transform.position;
        ui = FindFirstObjectByType<RobberUI>();
        SetupFSM();
    }

    void Update()
    {
        fsm.Tick();

        // Rotación suave manual: gira hacia la dirección real de movimiento
        if (Agent.velocity.sqrMagnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(Agent.velocity.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                angularSpeed * Time.deltaTime * Mathf.Deg2Rad);
        }

        // TECLA DE DEBUG: F9 fuerza el panel de victoria
        if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
            ui?.ShowVictory();

        if (!HasOrder && Keyboard.current != null)
        {
            if (Keyboard.current.digit0Key.wasPressedThisFrame) SelectGem();
            if (Keyboard.current.digit1Key.wasPressedThisFrame && paintings.Count > 0) SelectPainting(0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame && paintings.Count > 1) SelectPainting(1);
            if (Keyboard.current.digit3Key.wasPressedThisFrame && paintings.Count > 2) SelectPainting(2);
            if (Keyboard.current.digit4Key.wasPressedThisFrame && paintings.Count > 3) SelectPainting(3);
            if (Keyboard.current.digit5Key.wasPressedThisFrame && paintings.Count > 4) SelectPainting(4);
        }
    }

    // ── Selección de objetivo ─────────────────────────────────
    public void SelectGem() => SelectTarget(gem);
    public void SelectPainting(int i) => SelectTarget(paintings[i]);

    public void SelectTarget(GameObject target)
    {
        if (HasOrder) return;

        string nombre = target.name;

        if (stolenObjects.Contains(target))
        {
            Debug.Log($"[Input] '{nombre}' ya fue robado, elige otro.");
            ui?.ShowAlreadyStolen(nombre);
            return;
        }

        CurrentTarget = target.transform;
        CurrentTargetName = nombre;
        HasOrder = true;
        ui?.SetObjetivoText(nombre);
        Debug.Log($"[Input] Objetivo: {nombre}");
    }

    // Llamado desde DoneState cuando ya no quedan objetivos
    public bool HayObjetivosDisponibles()
    {
        if (gem != null && !stolenObjects.Contains(gem)) return true;
        foreach (var p in paintings)
            if (p != null && !stolenObjects.Contains(p)) return true;
        return false;
    }

    // Reseteos individuales llamados en OnExit de cada estado
    public void ResetEnteredGallery() { EnteredGallery = false; }
    public void ResetTargetStolen() { TargetStolen = false; }
    public void ResetEscaped() { Escaped = false; }

    // Llamado desde EscapingState al llegar a la van
    public void LlegarAVan()
    {
        Debug.Log("[FSM] Llegó a la van.");
        // Marcar en verde el objeto recién entregado
        if (CurrentTarget != null) ui?.MarkRobado(CurrentTarget.gameObject.name);
        ui?.SetObjetivoText(null);  // vuelve a "Objetivo: ninguno"

        // Comprobar victoria ANTES de resetear
        if (!HayObjetivosDisponibles())
        {
            ui?.ShowVictory();
            return;  // no resetear: el juego termina aquí
        }

        // Resetear flags para poder volver a entrar
        HasOrder = false;
        EnteredGallery = false;
        TargetStolen = false;
        Escaped = false;
        CurrentTarget = null;
    }

    // Llamado desde StealingTargetState cuando roba el objeto
    public void RegistrarRobo()
    {
        if (CurrentTarget != null)
            stolenObjects.Add(CurrentTarget.gameObject);
    }

    // ── Velocidad ─────────────────────────────────────────────
    public void SetOutsideSpeed() => Agent.speed = outsideSpeed;
    public void SetStealthSpeed() => Agent.speed = stealthSpeed;
    public void SetEscapeSpeed() => Agent.speed = escapeSpeed;

    // ── Helpers ──────────────────────────────────────────────
    public bool IsNear(Transform target, float extraMargin = 0f) =>
        target != null &&
        Vector3.Distance(transform.position, target.position) < arrivalThreshold + extraMargin;

    public bool IsNearBackDoor()
    {
        if (backDoor == null || !backDoor.activeSelf) return false;
        return Vector3.Distance(transform.position, backDoor.transform.position) < arrivalThreshold + 1f;
    }

    public void DestroyFrontDoor()
    {
        if (frontDoor != null && frontDoor.activeSelf)
            StartCoroutine(DoorRoutine(frontDoor));
    }

    public void DestroyBackDoor()
    {
        if (backDoor != null && backDoor.activeSelf)
            StartCoroutine(DoorRoutine(backDoor));
    }

    System.Collections.IEnumerator DoorRoutine(GameObject door)
    {
        door.SetActive(false);
        Debug.Log($"[Door] {door.name} desactivada.");
        yield return new WaitForSeconds(2f);
        door.SetActive(true);
        Debug.Log($"[Door] {door.name} reactivada.");
    }

    // ── Build Steal Tree ─────────────────────────────────────
    public void BuildStealTree()
    {
        var seq = new Sequence("StealSeq");
        seq.AddChild(new Leaf("CheckActive",
            new Condition(() => CurrentTarget != null && CurrentTarget.gameObject.activeSelf)));
        seq.AddChild(new Leaf("MoveToTarget",
            new MoveToTarget(transform, Agent, CurrentTarget, stealReach, horizontalOnly: true)));
        seq.AddChild(new Leaf("Steal",
            new ActionStrategy(() =>
            {
                CurrentTarget.gameObject.SetActive(false);
                RegistrarRobo();
                Debug.Log($"[BT] ¡{CurrentTargetName} robado!");
            })));
        StealTree = new BehaviourTree("StealTarget");
        StealTree.AddChild(seq);
    }


    // ── FSM ──────────────────────────────────────────────────
    void SetupFSM()
    {
        fsm = new StateMachine();

        var idle = new IdleState(this);
        var entering = new EnteringGalleryState(this);
        var stealing = new StealingTargetState(this);
        var escaping = new EscapingState(this);
        var done = new DoneState();

        fsm.AddTransition(idle, entering, () => HasOrder);
        fsm.AddTransition(entering, stealing, () => EnteredGallery);
        fsm.AddTransition(stealing, escaping, () => TargetStolen);

        // Al escapar: si llegó a la van (Escaped=true) y quedan objetos → Idle
        //             si llegó a la van y no quedan objetos → Done
        fsm.AddTransition(escaping, idle, () => Escaped && HayObjetivosDisponibles());
        fsm.AddTransition(escaping, done, () => Escaped && !HayObjetivosDisponibles());

        fsm.SetState(idle);
    }

    void OnDrawGizmosSelected()
    {
        if (waypoints == null) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Count - 1; i++)
            if (waypoints[i] && waypoints[i + 1])
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
    }
}