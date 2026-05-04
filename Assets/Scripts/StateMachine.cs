using System;
using System.Collections.Generic;
using UnityEngine;

namespace PathFinding.FSM
{
    /// <summary>
    /// Finite State Machine genérica. Cada estado es un objeto IState.
    /// </summary>
    public interface IState
    {
        void OnEnter();
        void OnUpdate();
        void OnExit();
    }

    public class StateMachine
    {
        IState currentState;
        Dictionary<Type, List<Transition>> transitions = new();
        List<Transition> currentTransitions = new();
        List<Transition> anyTransitions = new();

        static readonly List<Transition> EmptyTransitions = new();

        public IState CurrentState => currentState;

        public void Tick()
        {
            var transition = GetTriggeredTransition();
            if (transition != null)
                SetState(transition.To);

            currentState?.OnUpdate();
        }

        public void SetState(IState state)
        {
            if (state == currentState) return;

            currentState?.OnExit();
            currentState = state;

            transitions.TryGetValue(currentState.GetType(), out currentTransitions);
            currentTransitions ??= EmptyTransitions;

            currentState.OnEnter();
        }

        /// <summary>Añade una transición desde un estado concreto.</summary>
        public void AddTransition(IState from, IState to, Func<bool> condition)
        {
            if (!transitions.TryGetValue(from.GetType(), out var list))
            {
                list = new List<Transition>();
                transitions[from.GetType()] = list;
            }
            list.Add(new Transition(to, condition));
        }

        /// <summary>Añade una transición desde cualquier estado.</summary>
        public void AddAnyTransition(IState to, Func<bool> condition) =>
            anyTransitions.Add(new Transition(to, condition));

        Transition GetTriggeredTransition()
        {
            foreach (var t in anyTransitions)
                if (t.Condition()) return t;

            foreach (var t in currentTransitions)
                if (t.Condition()) return t;

            return null;
        }

        class Transition
        {
            public IState To { get; }
            public Func<bool> Condition { get; }
            public Transition(IState to, Func<bool> condition) { To = to; Condition = condition; }
        }
    }
}