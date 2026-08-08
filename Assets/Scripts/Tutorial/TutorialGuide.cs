using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Collections;
using DG.Tweening; // Оставляем для DOMove и DOFade

namespace Assets.Scripts.Tutorial
{
    public class TutorialGuide : MonoBehaviour
    {
        [Header("UI элементы")]
        public CanvasGroup dialogueCanvasGroup;
        public TextMeshProUGUI dialogueText;
        public GameObject nextButtonObject;

        [Header("Анимации")]
        public GameObject meepleModel;
        public Transform visiblePosition;
        public Transform hiddenPosition;

        [Header("Настройки печати")]
        public float defaultFontSize = 3.54f; // Базовый размер шрифта
        public float timePerCharacter = 0.03f; // Скорость появления букв (меньше = быстрее)

        private Queue<string> _currentSentences = new Queue<string>();
        private Action _onSequenceComplete;
        private Coroutine _typingCoroutine;

        void Awake()
        {
            nextButtonObject.GetComponent<Button>().onClick.AddListener(ShowNextSentence);
            meepleModel.transform.position = hiddenPosition.position;
            dialogueCanvasGroup.alpha = 0f;
            nextButtonObject.SetActive(false);
        }

        /// <summary>
        /// Метод запуска монолога гида.
        /// </summary>
        /// <param name="sentences">Предложения, которые пишет гид</param>
        /// <param name="onComplete"></param>
        public void StartDialogue(string[] sentences, Action onComplete)
        {
            _currentSentences.Clear();
            foreach (string sentence in sentences)
            {
                _currentSentences.Enqueue(sentence);
            }

            _onSequenceComplete = onComplete;
            dialogueText.text = "";
            nextButtonObject.SetActive(false);

            StartCoroutine(EntranceRoutine());
        }

        private IEnumerator EntranceRoutine()
        {
            meepleModel.transform.DOMove(visiblePosition.position, 1.0f).SetEase(Ease.OutBack);
            yield return new WaitForSeconds(0.5f);

            dialogueCanvasGroup.DOFade(1f, 0.3f);
            yield return new WaitForSeconds(0.4f);

            ShowNextSentence();
        }

        private void ShowNextSentence()
        {
            nextButtonObject.SetActive(false);

            if (_currentSentences.Count == 0)
            {
                StartCoroutine(ExitRoutine());
                return;
            }

            string fullSentence = _currentSentences.Dequeue();

            if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
            _typingCoroutine = StartCoroutine(TypeSentenceRoutine(fullSentence));
        }

        /// <summary>
        /// Корутина, которая создаёт эффект пишущей машинки.
        /// </summary>
        /// <param name="sentence"></param>
        /// <returns></returns>
        private IEnumerator TypeSentenceRoutine(string sentence)
        {
            // Сбрасываем размер шрифта перед каждым новым предложением
            dialogueText.fontSize = defaultFontSize;

            dialogueText.text = "";

            foreach (char letter in sentence.ToCharArray())
            {
                dialogueText.text += letter;
                yield return new WaitForSeconds(timePerCharacter);
            }

            yield return new WaitForSeconds(0.5f);

            nextButtonObject.SetActive(true);
        }

        
        /// <summary>
        /// Метод, который вызывается при совершении ошибки игрока в обучении.
        /// </summary>
        /// <param name="hintText"></param>
        public void ShowHint(string hintText)
        {
            _currentSentences.Clear();

            if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);

            if (transform.position == visiblePosition.position)
            {
                nextButtonObject.SetActive(false);
                _typingCoroutine = StartCoroutine(TypeSentenceRoutineNoButton(hintText));
            }
            else
            {
                _currentSentences.Enqueue(hintText);
                StartCoroutine(EntranceRoutineNoButton());
            }
        }

        // Такая же корутина, но при печати текста подсказок без кнопки "Далее"
        private IEnumerator TypeSentenceRoutineNoButton(string sentence)
        {
            // ИСПРАВЛЕНИЕ: Сбрасываем размер шрифта для подсказок
            dialogueText.fontSize = defaultFontSize;

            dialogueText.text = "";

            foreach (char letter in sentence.ToCharArray())
            {
                dialogueText.text += letter;
                yield return new WaitForSeconds(timePerCharacter);
            }
        }

        /// <summary>
        /// Корутина, запускающаяся перед началом монолога гида.
        /// </summary>
        /// <returns></returns>
        private IEnumerator EntranceRoutineNoButton()
        {
            meepleModel.transform.DOMove(visiblePosition.position, 1.5f).SetEase(Ease.OutBack);
            yield return new WaitForSeconds(0.5f);

            dialogueCanvasGroup.DOFade(1f, 0.3f);
            yield return new WaitForSeconds(0.4f);

            string hint = _currentSentences.Dequeue();
            _typingCoroutine = StartCoroutine(TypeSentenceRoutineNoButton(hint));
        }

        private IEnumerator ExitRoutine()
        {
            dialogueCanvasGroup.DOFade(0f, 0.3f);
            yield return new WaitForSeconds(0.3f);

            meepleModel.transform.DOMove(hiddenPosition.position, 1.5f).SetEase(Ease.InBack);
            yield return new WaitForSeconds(0.5f);

            _onSequenceComplete?.Invoke();
        }
    }
}