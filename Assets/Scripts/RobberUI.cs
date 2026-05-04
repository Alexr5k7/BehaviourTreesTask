using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Gestiona dos elementos de UI:
///
/// 1. TEXTO DE AVISO (rojo, parte inferior)
///    Aparece 2s cuando se intenta robar algo ya robado.
///    Asigna en el inspector: WarningText (TMP_Text)
///
/// 2. PANEL DE VICTORIA
///    Aparece cuando se han robado todos los objetos.
///    Tiene un botón "Volver a empezar" que recarga la escena.
///    Asigna en el inspector: VictoryPanel (GameObject con Image)
///    y dentro de él: VictoryButton (Button)
///
/// SETUP RÁPIDO EN UNITY:
///   - Crea un Canvas (Screen Space - Overlay).
///   - Hijo 1: Text (TMP) centrado abajo → asígnalo a WarningText.
///   - Hijo 2: Panel vacío con Image oscura → asígnalo a VictoryPanel.
///       - Dentro del panel: Text TMP con "¡ROBO COMPLETADO!"
///       - Dentro del panel: Button → asígnalo a VictoryButton.
///   - Añade este script al Canvas y arrastra RobberController.
/// </summary>
public class RobberUI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] RobberController robber;

    [Header("Aviso 'ya robado'")]
    [SerializeField] TMP_Text warningText;
    [SerializeField] float warningDuration = 2f;

    [Header("Panel de Victoria")]
    [SerializeField] GameObject victoryPanel;
    [SerializeField] Button victoryButton;

    [Header("Texto de objetivo")]
    [SerializeField] TMP_Text objetivoText;

    [Header("Lista de objetivos")]
    [SerializeField] Transform listaContainer;
    [SerializeField] Color colorPendiente = new Color(0.9f, 0.2f, 0.2f, 1f);
    [SerializeField] Color colorRobado = new Color(0.2f, 0.85f, 0.2f, 1f);

    Coroutine warningCoroutine;
    bool victoryShown;

    // Mapa nombre → texto de la lista
    readonly System.Collections.Generic.Dictionary<string, TMP_Text> listaItems = new();

    // ── Unity ────────────────────────────────────────────────
    void Start()
    {
        // Asegurarse de que todo empieza oculto
        if (warningText != null) warningText.gameObject.SetActive(false);
        if (objetivoText != null) objetivoText.text = "Objetivo: ninguno";
        if (victoryPanel != null) victoryPanel.SetActive(false);

        // Botón de reinicio
        if (victoryButton != null)
            victoryButton.onClick.AddListener(ResetScene);

        BuildLista();
    }

    // ── API pública llamada desde RobberController ────────────
    /// Construye la lista de objetivos en runtime a partir de los objetos del RobberController.
    void BuildLista()
    {
        if (listaContainer == null || robber == null) return;

        var objetos = new System.Collections.Generic.List<GameObject>();
        if (robber.gem != null) objetos.Add(robber.gem);
        foreach (var p in robber.paintings)
            if (p != null) objetos.Add(p);

        foreach (var obj in objetos)
        {
            // Crear Text TMP hijo del contenedor
            var go = new GameObject(obj.name, typeof(RectTransform), typeof(CanvasRenderer),
                                    typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(listaContainer, false);

            var tmp = go.GetComponent<TMPro.TextMeshProUGUI>();
            tmp.text = obj.name;
            tmp.fontSize = 18;
            tmp.color = colorPendiente;
            tmp.alignment = TMPro.TextAlignmentOptions.Left;

            listaItems[obj.name] = tmp;
        }
    }

    /// Marca un objeto como robado (verde) en la lista.
    public void MarkRobado(string nombre)
    {
        if (listaItems.TryGetValue(nombre, out var tmp))
            tmp.color = colorRobado;
    }

    /// Muestra el aviso "Ya ha sido robado" durante warningDuration segundos.
    public void ShowAlreadyStolen(string label)
    {
        if (warningText == null) return;

        if (warningCoroutine != null)
            StopCoroutine(warningCoroutine);

        warningCoroutine = StartCoroutine(WarningRoutine(label));
    }

    /// Actualiza el texto "Objetivo: X" en la esquina inferior derecha.
    /// Pasar null o string vacío para mostrar "Objetivo: ninguno".
    public void SetObjetivoText(string nombre)
    {
        if (objetivoText == null) return;
        objetivoText.text = string.IsNullOrEmpty(nombre)
            ? "Objetivo: ninguno"
            : $"Objetivo: {nombre}";
    }

    // ── Privado ──────────────────────────────────────────────
    IEnumerator WarningRoutine(string label)
    {
        warningText.text = $"'{label}' ya ha sido robado, elige otro";
        warningText.gameObject.SetActive(true);
        yield return new WaitForSeconds(warningDuration);
        warningText.gameObject.SetActive(false);
    }

    public void ShowVictory()
    {
        victoryShown = true;
        Time.timeScale = 0f;   // pausa el juego
        if (victoryPanel != null) victoryPanel.SetActive(true);
        Debug.Log("[UI] ¡Victoria! Todos los objetos robados.");
    }

    void ResetScene()
    {
        Time.timeScale = 1f;   // restaurar antes de recargar
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}