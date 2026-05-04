using System.Collections.Generic;
using PathFinding.BehaviourTrees;
using UnityEngine;


[RequireComponent(typeof(UnityEngine.AI.NavMeshAgent))]
public class NPCScript : MonoBehaviour

{
    [SerializeField] List<Transform> waypoints = new();
    [SerializeField] GameObject monaLisa;
    [SerializeField] GameObject gem;
    [SerializeField] GameObject van;
    UnityEngine.AI.NavMeshAgent agent;
    BehaviourTree tree;


    void Awake()
    {
        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();

    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        tree = new BehaviourTree("NPC");
        Leaf moveToMonaLisa = new Leaf("MoveToMonaLisa", new MoveToTarget(transform, agent, monaLisa.transform));
        tree.AddChild(moveToMonaLisa);

        /*
        //Patrolling
        //Leaf patrol = new Leaf("Patrol", new PatrolStrategy(transform, agent, waypoints));
        //tree.AddChild(patrol);
        Leaf isMonaLisaPresent = new Leaf("IsMonaLisaPresent", new Condition(() => monaLisa.activeSelf));
        Leaf moveToMonaLisa = new Leaf("MoveToMonaLisa", new ActionStrategy(() => agent.SetDestination(monaLisa.transform.position)));

        
        Sequence goToMonaLisa = new Sequence("GoToMonalisa");
        goToMonaLisa.AddChild(isMonaLisaPresent);
        goToMonaLisa.AddChild(moveToMonaLisa);
        //tree.AddChild(goToMonaLisa);

        Leaf isGemPresent = new Leaf("IsGemPresent", new Condition(() => gem.activeSelf));
        Leaf moveToGem = new Leaf("MoveToGem", new ActionStrategy(() => agent.SetDestination(gem.transform.position)));

        Sequence goToGem = new Sequence("GoToGem");
        goToGem.AddChild(isGemPresent);
        goToGem.AddChild(moveToGem);
        //tree.AddChild(goToGem);

        //Two sequences, one selector
        Selector goToSteal = new Selector("GoToSteal");
        goToSteal.AddChild(goToGem);
        goToSteal.AddChild(goToMonaLisa);

        tree.AddChild(goToSteal);
        */

    }

    // Update is called once per frame
    void Update()
    {
        Node.Status ns = tree.Process();

    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name.Equals("GetMonaLisa"))
        {
            monaLisa.SetActive(false);
        }
    }
}
