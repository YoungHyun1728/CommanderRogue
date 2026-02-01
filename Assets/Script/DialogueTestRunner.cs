using UnityEngine;

public class DialogueTestRunner : MonoBehaviour
{
    [SerializeField] private DialogueDefinition testDialogue;

    void Update()
    {
        // F1 누르면 테스트 대화 시작
        if (Input.GetKeyDown(KeyCode.F1))
        {
            if (testDialogue == null) return;

            var ctx = new DialogueContext { runManager = RunManager.Instance };
            DialogueManager.Instance.StartDialogue(testDialogue, ctx);
        }
    }
}