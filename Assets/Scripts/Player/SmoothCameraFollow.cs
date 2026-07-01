using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Grid;
using Laser;

namespace Player
{
    [RequireComponent(typeof(Camera))]
    public class SmoothCameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private PlayerGridController playerController;

        [Header("Follow")]
        [SerializeField] private float smoothTime = 0.12f;
        [SerializeField] private float maxSpeed = 100f;
        [SerializeField] private bool snapOnStart = true;

        [Header("Laser Path Framing")]
        [SerializeField] private bool zoomOutToFitLaserPath = true;
        [SerializeField] private LaserRenderer laserRenderer;
        [SerializeField, Range(0f, 0.45f)] private float laserViewportMargin = 0.12f;
        [SerializeField] private float laserPathWorldPadding = 0.35f;
        [SerializeField] private float laserFramePositionSmoothTime = 0.055f;
        [SerializeField] private float laserFrameZoomSmoothTime = 0.055f;
        [SerializeField] private float laserFrameMaxSpeed = 180f;
        [SerializeField] private float maxLaserFrameOrthographicSize = 16f;

        [Header("Clear Hole Focus")]
        [SerializeField] private bool focusClearHoleOnAppear = true;
        [SerializeField] private float clearHoleZoomOrthographicSize = 2.4f;
        [SerializeField] private float clearHoleFocusInDuration = 0.28f;
        [SerializeField] private float clearHoleHoldDuration = 2f;
        [SerializeField] private float clearHoleReturnDuration = 0.45f;
        [SerializeField] private bool suppressLaserFramingAfterClearHoleAppears = true;
        [SerializeField] private float playerMoveCancelThreshold = 0.05f;
        [SerializeField] private bool resetZoomImmediatelyWhenPlayerMovesDuringHoleFocus = false;
        [SerializeField] private AnimationCurve clearHoleFocusCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Map Overview")]
        [SerializeField] private bool zoomOutToFullMapWhileHoldingS = true;
        [SerializeField] private float fullMapWorldPadding = 0.8f;
        [SerializeField] private float fullMapPositionSmoothTime = 0.055f;
        [SerializeField] private float fullMapZoomSmoothTime = 0.055f;
        [SerializeField] private float fullMapMaxSpeed = 220f;
        [SerializeField] private float maxFullMapOrthographicSize = 30f;

        [Header("Stage Start Intro")]
        [SerializeField] private bool playStageStartZoomIntro = true;
        [SerializeField] private float stageStartZoomOrthographicSize = 2.2f;
        [SerializeField] private bool stageStartRevealFullMap = true;
        [SerializeField] private float stageStartZoomOutDuration = 0.7f;
        [SerializeField] private float stageStartMapHoldDuration = 0.25f;
        [SerializeField] private float stageStartReturnDuration = 0.45f;
        [SerializeField] private float stageStartFullMapPadding = 0.8f;
        [SerializeField] private AnimationCurve stageStartIntroCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Camera Shake")]
        [SerializeField] private float defaultShakeDuration = 0.18f;
        [SerializeField] private float defaultShakeStrength = 0.22f;
        [SerializeField] private float defaultShakeFrequency = 48f;

        [Header("Axis")]
        [SerializeField] private bool followX = true;
        [SerializeField] private bool followY = true;
        [SerializeField] private bool keepInitialZ = true;

        [Header("Map Bounds")]
        [SerializeField] private bool useGridBounds = true;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private float boundsPadding = 0.5f;

        private Camera targetCamera;
        private Vector3 velocity;
        private float zoomVelocity;
        private float fixedZ;
        private float defaultOrthographicSize;
        private bool isClearHoleFocusPlaying;
        private bool suppressLaserFraming;
        private Coroutine clearHoleFocusRoutine;
        private Vector3 clearHoleFocusTargetStartPosition;
        private bool isStageStartIntroPlaying;
        private Coroutine cameraShakeRoutine;
        private Vector3 currentShakeOffset;
        private Vector3 appliedShakeOffset;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            fixedZ = transform.position.z;
            defaultOrthographicSize = targetCamera != null ? targetCamera.orthographicSize : 5f;

            if (playerController == null)
                playerController = FindFirstObjectByType<PlayerGridController>();

            if (target == null && playerController != null)
                target = playerController.transform;

            if (gridManager == null)
                gridManager = FindFirstObjectByType<GridManager>();

            if (laserRenderer == null)
                laserRenderer = FindFirstObjectByType<LaserRenderer>();
        }

        private void Start()
        {
            if (snapOnStart)
                SnapToTarget();
        }

        private void LateUpdate()
        {
            if (appliedShakeOffset != Vector3.zero)
            {
                transform.position -= appliedShakeOffset;
                appliedShakeOffset = Vector3.zero;
            }

            if (target == null || targetCamera == null)
                return;

            if (isStageStartIntroPlaying)
            {
                ApplyCameraShakeOffset();
                return;
            }

            if (isClearHoleFocusPlaying)
            {
                if (HasPlayerMovedDuringClearHoleFocus())
                    CancelClearHoleFocus(true);
                else
                {
                    ApplyCameraShakeOffset();
                    return;
                }
            }

            Vector3 desiredPosition = GetDesiredPosition();
            float desiredOrthographicSize = defaultOrthographicSize;
            bool framingLaserPath = false;
            bool showingFullMap = IsFullMapOverviewHeld();

            if (showingFullMap)
                ApplyFullMapOverview(ref desiredPosition, ref desiredOrthographicSize);
            else
                framingLaserPath = TryApplyLaserPathFrame(ref desiredPosition, ref desiredOrthographicSize);

            desiredPosition = ClampToGridBounds(desiredPosition, desiredOrthographicSize);

            float currentSmoothTime = showingFullMap ? fullMapPositionSmoothTime : framingLaserPath ? laserFramePositionSmoothTime : smoothTime;
            float currentMaxSpeed = showingFullMap ? fullMapMaxSpeed : framingLaserPath ? laserFrameMaxSpeed : maxSpeed;
            float currentZoomSmoothTime = showingFullMap ? fullMapZoomSmoothTime : framingLaserPath ? laserFrameZoomSmoothTime : smoothTime;

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, currentSmoothTime, currentMaxSpeed, Time.deltaTime);
            targetCamera.orthographicSize = Mathf.SmoothDamp(targetCamera.orthographicSize, desiredOrthographicSize, ref zoomVelocity, currentZoomSmoothTime, Mathf.Infinity, Time.deltaTime);
            ApplyCameraShakeOffset();
        }

        public void SetTarget(Transform newTarget, bool snap = true)
        {
            target = newTarget;
            velocity = Vector3.zero;
            zoomVelocity = 0f;

            if (snap)
                SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (target == null || targetCamera == null)
                return;

            Vector3 desiredPosition = GetDesiredPosition();
            float desiredOrthographicSize = defaultOrthographicSize;
            TryApplyLaserPathFrame(ref desiredPosition, ref desiredOrthographicSize);
            desiredPosition = ClampToGridBounds(desiredPosition, desiredOrthographicSize);

            transform.position = desiredPosition;
            targetCamera.orthographicSize = desiredOrthographicSize;
            velocity = Vector3.zero;
            zoomVelocity = 0f;
        }

        public void PlayClearHoleFocus(Vector3 holeWorldPosition)
        {
            PlayClearHoleFocus(holeWorldPosition, clearHoleHoldDuration);
        }

        public void PlayClearHoleFocus(Vector3 holeWorldPosition, float holdDurationOverride)
        {
            if (!focusClearHoleOnAppear || targetCamera == null)
                return;

            if (clearHoleFocusRoutine != null)
                StopCoroutine(clearHoleFocusRoutine);

            clearHoleFocusRoutine = StartCoroutine(ClearHoleFocusRoutine(holeWorldPosition, holdDurationOverride));
        }

        public void CancelClearHoleFocus()
        {
            CancelClearHoleFocus(false);
        }

        private void CancelClearHoleFocus(bool restoreDefaultZoom)
        {
            if (clearHoleFocusRoutine != null)
            {
                StopCoroutine(clearHoleFocusRoutine);
                clearHoleFocusRoutine = null;
            }

            isClearHoleFocusPlaying = false;
            suppressLaserFraming = false;
            velocity = Vector3.zero;
            zoomVelocity = 0f;

            // 이동으로 구멍 포커스를 취소할 때는 즉시 줌을 풀지 않고,
            // 다음 LateUpdate의 SmoothDamp가 기본 카메라 크기까지 부드럽게 복귀시킨다.
            // resetZoomImmediatelyWhenPlayerMovesDuringHoleFocus 값은 예전 씬 직렬화 호환용으로만 남겨둔다.
        }

        public void ResetToDefaultSize(bool snap = false)
        {
            if (targetCamera == null)
                return;

            if (snap)
            {
                targetCamera.orthographicSize = defaultOrthographicSize;
                zoomVelocity = 0f;
            }
        }

        public void PrepareStageStartZoom(Vector3 focusWorldPosition)
        {
            if (!playStageStartZoomIntro || targetCamera == null)
                return;

            CancelClearHoleFocus(false);
            isStageStartIntroPlaying = true;
            suppressLaserFraming = true;
            velocity = Vector3.zero;
            zoomVelocity = 0f;

            float introSize = Mathf.Clamp(stageStartZoomOrthographicSize, 0.1f, defaultOrthographicSize);
            Vector3 focusPosition = new Vector3(focusWorldPosition.x, focusWorldPosition.y, keepInitialZ ? fixedZ : transform.position.z);
            focusPosition = ClampToGridBounds(focusPosition, introSize);
            transform.position = focusPosition;
            targetCamera.orthographicSize = introSize;
        }

        public IEnumerator PlayStageStartRevealRoutine()
        {
            if (!playStageStartZoomIntro || targetCamera == null)
            {
                isStageStartIntroPlaying = false;
                suppressLaserFraming = false;
                yield break;
            }

            Vector3 startPosition = transform.position;
            float startSize = targetCamera.orthographicSize;
            Vector3 revealPosition = target != null ? GetDesiredPosition() : startPosition;
            float revealSize = defaultOrthographicSize;

            if (stageStartRevealFullMap && TryGetGridWorldBounds(out Bounds mapBounds))
            {
                if (stageStartFullMapPadding > 0f)
                    mapBounds.Expand(stageStartFullMapPadding * 2f);

                revealPosition = new Vector3(mapBounds.center.x, mapBounds.center.y, keepInitialZ ? fixedZ : transform.position.z);
                revealSize = CalculateOrthographicSizeToFitBounds(mapBounds, 0f);
                revealSize = Mathf.Clamp(revealSize, defaultOrthographicSize, Mathf.Max(defaultOrthographicSize, maxFullMapOrthographicSize));
            }

            revealPosition = ClampToGridBounds(revealPosition, revealSize);
            yield return AnimateStageStartCamera(startPosition, startSize, revealPosition, revealSize, stageStartZoomOutDuration, false);

            float hold = Mathf.Max(0f, stageStartMapHoldDuration);
            float holdElapsed = 0f;
            while (holdElapsed < hold)
            {
                holdElapsed += Time.deltaTime;
                transform.position = revealPosition;
                targetCamera.orthographicSize = revealSize;
                yield return null;
            }

            Vector3 returnStartPosition = transform.position;
            float returnStartSize = targetCamera.orthographicSize;
            Vector3 returnPosition = target != null ? ClampToGridBounds(GetDesiredPosition(), defaultOrthographicSize) : returnStartPosition;
            yield return AnimateStageStartCamera(returnStartPosition, returnStartSize, returnPosition, defaultOrthographicSize, stageStartReturnDuration, true);

            if (target != null)
                transform.position = ClampToGridBounds(GetDesiredPosition(), defaultOrthographicSize);

            targetCamera.orthographicSize = defaultOrthographicSize;
            velocity = Vector3.zero;
            zoomVelocity = 0f;
            isStageStartIntroPlaying = false;
            suppressLaserFraming = false;
        }

        public void PlayShake()
        {
            PlayShake(defaultShakeDuration, defaultShakeStrength, defaultShakeFrequency);
        }

        public void PlayShake(float duration, float strength)
        {
            PlayShake(duration, strength, defaultShakeFrequency);
        }

        public void PlayShake(float duration, float strength, float frequency)
        {
            if (targetCamera == null)
                return;

            if (cameraShakeRoutine != null)
                StopCoroutine(cameraShakeRoutine);

            cameraShakeRoutine = StartCoroutine(CameraShakeRoutine(duration, strength, frequency));
        }

        private IEnumerator ClearHoleFocusRoutine(Vector3 holeWorldPosition, float holdDuration)
        {
            isClearHoleFocusPlaying = true;
            clearHoleFocusTargetStartPosition = target != null ? target.position : transform.position;

            if (suppressLaserFramingAfterClearHoleAppears)
                suppressLaserFraming = true;

            velocity = Vector3.zero;
            zoomVelocity = 0f;

            Vector3 startPosition = transform.position;
            float startSize = targetCamera.orthographicSize;
            float focusSize = Mathf.Max(0.1f, clearHoleZoomOrthographicSize);
            Vector3 focusPosition = new Vector3(holeWorldPosition.x, holeWorldPosition.y, keepInitialZ ? fixedZ : transform.position.z);
            focusPosition = ClampToGridBounds(focusPosition, focusSize);

            yield return AnimateCamera(startPosition, startSize, focusPosition, focusSize, clearHoleFocusInDuration, false);

            float holdTime = 0f;
            while (holdTime < Mathf.Max(0f, holdDuration))
            {
                if (HasPlayerMovedDuringClearHoleFocus())
                {
                    CancelClearHoleFocus(true);
                    yield break;
                }

                holdTime += Time.deltaTime;
                transform.position = focusPosition;
                targetCamera.orthographicSize = focusSize;
                yield return null;
            }

            Vector3 returnStartPosition = transform.position;
            float returnStartSize = targetCamera.orthographicSize;
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, clearHoleReturnDuration);

            while (elapsed < duration)
            {
                if (HasPlayerMovedDuringClearHoleFocus())
                {
                    CancelClearHoleFocus(true);
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = EvaluateClearHoleCurve(Mathf.Clamp01(elapsed / duration));
                Vector3 returnPosition = target != null ? GetDesiredPosition() : returnStartPosition;
                returnPosition = ClampToGridBounds(returnPosition, defaultOrthographicSize);
                transform.position = Vector3.LerpUnclamped(returnStartPosition, returnPosition, t);
                targetCamera.orthographicSize = Mathf.LerpUnclamped(returnStartSize, defaultOrthographicSize, t);
                yield return null;
            }

            if (target != null)
                transform.position = ClampToGridBounds(GetDesiredPosition(), defaultOrthographicSize);

            targetCamera.orthographicSize = defaultOrthographicSize;
            velocity = Vector3.zero;
            zoomVelocity = 0f;
            isClearHoleFocusPlaying = false;
            clearHoleFocusRoutine = null;
        }

        private IEnumerator AnimateCamera(Vector3 fromPosition, float fromSize, Vector3 toPosition, float toSize, float duration, bool dynamicTarget)
        {
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);

            while (elapsed < duration)
            {
                if (isClearHoleFocusPlaying && HasPlayerMovedDuringClearHoleFocus())
                {
                    CancelClearHoleFocus(true);
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = EvaluateClearHoleCurve(Mathf.Clamp01(elapsed / duration));
                Vector3 currentTargetPosition = dynamicTarget && target != null ? ClampToGridBounds(GetDesiredPosition(), toSize) : toPosition;
                transform.position = Vector3.LerpUnclamped(fromPosition, currentTargetPosition, t);
                targetCamera.orthographicSize = Mathf.LerpUnclamped(fromSize, toSize, t);
                yield return null;
            }

            transform.position = toPosition;
            targetCamera.orthographicSize = toSize;
        }

        private IEnumerator AnimateStageStartCamera(Vector3 fromPosition, float fromSize, Vector3 toPosition, float toSize, float duration, bool dynamicTarget)
        {
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EvaluateStageStartCurve(Mathf.Clamp01(elapsed / duration));
                Vector3 currentTargetPosition = dynamicTarget && target != null ? ClampToGridBounds(GetDesiredPosition(), toSize) : toPosition;
                transform.position = Vector3.LerpUnclamped(fromPosition, currentTargetPosition, t);
                targetCamera.orthographicSize = Mathf.LerpUnclamped(fromSize, toSize, t);
                yield return null;
            }

            transform.position = dynamicTarget && target != null ? ClampToGridBounds(GetDesiredPosition(), toSize) : toPosition;
            targetCamera.orthographicSize = toSize;
        }

        private IEnumerator CameraShakeRoutine(float duration, float strength, float frequency)
        {
            duration = Mathf.Max(0.01f, duration);
            strength = Mathf.Max(0f, strength);
            frequency = Mathf.Max(1f, frequency);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float fade = 1f - normalized;
                float seed = Time.unscaledTime * frequency;
                float x = (Mathf.PerlinNoise(seed, 0.17f) * 2f - 1f) * strength * fade;
                float y = (Mathf.PerlinNoise(0.73f, seed) * 2f - 1f) * strength * fade;
                currentShakeOffset = new Vector3(x, y, 0f);
                yield return null;
            }

            currentShakeOffset = Vector3.zero;
            cameraShakeRoutine = null;
        }

        private void ApplyCameraShakeOffset()
        {
            if (currentShakeOffset == Vector3.zero)
                return;

            transform.position += currentShakeOffset;
            appliedShakeOffset = currentShakeOffset;
        }

        private float EvaluateClearHoleCurve(float t)
        {
            if (clearHoleFocusCurve == null)
                return t;

            return clearHoleFocusCurve.Evaluate(t);
        }

        private float EvaluateStageStartCurve(float t)
        {
            if (stageStartIntroCurve == null)
                return t;

            return stageStartIntroCurve.Evaluate(t);
        }

        private bool HasPlayerMovedDuringClearHoleFocus()
        {
            if (!isClearHoleFocusPlaying || target == null)
                return false;

            if (playerController != null && playerController.IsMoving)
                return true;

            float threshold = Mathf.Max(0.001f, playerMoveCancelThreshold);
            return (target.position - clearHoleFocusTargetStartPosition).sqrMagnitude > threshold * threshold;
        }

        private bool IsFullMapOverviewHeld()
        {
            if (!zoomOutToFullMapWhileHoldingS)
                return false;

            if (Keyboard.current == null)
                return false;

            return Keyboard.current.qKey.isPressed;
        }

        private void ApplyFullMapOverview(ref Vector3 desiredPosition, ref float desiredOrthographicSize)
        {
            if (!TryGetGridWorldBounds(out Bounds mapBounds))
                return;

            if (fullMapWorldPadding > 0f)
                mapBounds.Expand(fullMapWorldPadding * 2f);

            Vector3 center = mapBounds.center;
            if (followX) desiredPosition.x = center.x;
            if (followY) desiredPosition.y = center.y;
            if (keepInitialZ) desiredPosition.z = fixedZ;

            desiredOrthographicSize = CalculateOrthographicSizeToFitBounds(mapBounds, 0f);
            desiredOrthographicSize = Mathf.Clamp(desiredOrthographicSize, defaultOrthographicSize, Mathf.Max(defaultOrthographicSize, maxFullMapOrthographicSize));
        }

        private bool TryGetGridWorldBounds(out Bounds bounds)
        {
            bounds = new Bounds(transform.position, Vector3.zero);

            if (gridManager == null || gridManager.CurrentStageData == null)
                return false;

            int width = gridManager.CurrentStageData.width;
            int height = gridManager.CurrentStageData.height;

            if (width <= 0 || height <= 0)
                return false;

            Vector3 bottomLeft = gridManager.GridToWorld(Vector2Int.zero);
            Vector3 topRight = gridManager.GridToWorld(new Vector2Int(width - 1, height - 1));
            Vector3 min = new Vector3(Mathf.Min(bottomLeft.x, topRight.x), Mathf.Min(bottomLeft.y, topRight.y), 0f);
            Vector3 max = new Vector3(Mathf.Max(bottomLeft.x, topRight.x), Mathf.Max(bottomLeft.y, topRight.y), 0f);
            bounds.SetMinMax(min, max);
            return true;
        }

        private Vector3 GetDesiredPosition()
        {
            Vector3 currentPosition = transform.position;
            Vector3 targetPosition = target.position;

            float x = followX ? targetPosition.x : currentPosition.x;
            float y = followY ? targetPosition.y : currentPosition.y;
            float z = keepInitialZ ? fixedZ : currentPosition.z;

            return new Vector3(x, y, z);
        }

        private bool TryApplyLaserPathFrame(ref Vector3 desiredPosition, ref float desiredOrthographicSize)
        {
            if (!zoomOutToFitLaserPath || suppressLaserFraming)
                return false;

            if (targetCamera == null || laserRenderer == null || !targetCamera.orthographic)
                return false;

            if (!laserRenderer.TryGetRenderedLaserBounds(out Bounds laserBounds))
                return false;

            if (target != null)
                laserBounds.Encapsulate(target.position);

            if (laserPathWorldPadding > 0f)
                laserBounds.Expand(laserPathWorldPadding * 2f);

            if (IsBoundsInsideCamera(laserBounds, desiredPosition, defaultOrthographicSize, laserViewportMargin))
                return false;

            Vector3 center = laserBounds.center;
            if (followX) desiredPosition.x = center.x;
            if (followY) desiredPosition.y = center.y;
            if (keepInitialZ) desiredPosition.z = fixedZ;

            desiredOrthographicSize = CalculateOrthographicSizeToFitBounds(laserBounds, laserViewportMargin);
            desiredOrthographicSize = Mathf.Clamp(desiredOrthographicSize, defaultOrthographicSize, Mathf.Max(defaultOrthographicSize, maxLaserFrameOrthographicSize));
            return true;
        }

        private bool IsBoundsInsideCamera(Bounds bounds, Vector3 cameraPosition, float orthographicSize, float margin)
        {
            if (targetCamera == null)
                return true;

            margin = Mathf.Clamp(margin, 0f, 0.45f);
            float halfHeight = Mathf.Max(0.01f, orthographicSize) * (1f - margin);
            float halfWidth = halfHeight * targetCamera.aspect;

            float minX = cameraPosition.x - halfWidth;
            float maxX = cameraPosition.x + halfWidth;
            float minY = cameraPosition.y - halfHeight;
            float maxY = cameraPosition.y + halfHeight;

            return bounds.min.x >= minX && bounds.max.x <= maxX && bounds.min.y >= minY && bounds.max.y <= maxY;
        }

        private float CalculateOrthographicSizeToFitBounds(Bounds bounds, float margin)
        {
            if (targetCamera == null)
                return defaultOrthographicSize;

            margin = Mathf.Clamp(margin, 0f, 0.45f);
            float visibleRatio = Mathf.Max(0.1f, 1f - margin);
            float heightSize = bounds.extents.y / visibleRatio;
            float widthSize = bounds.extents.x / Mathf.Max(0.01f, targetCamera.aspect * visibleRatio);
            return Mathf.Max(defaultOrthographicSize, heightSize, widthSize);
        }

        private Vector3 ClampToGridBounds(Vector3 desiredPosition, float orthographicSize)
        {
            if (!useGridBounds)
                return desiredPosition;

            if (gridManager == null || gridManager.CurrentStageData == null || targetCamera == null)
                return desiredPosition;

            if (!targetCamera.orthographic)
                return desiredPosition;

            int width = gridManager.CurrentStageData.width;
            int height = gridManager.CurrentStageData.height;

            if (width <= 0 || height <= 0)
                return desiredPosition;

            Vector3 bottomLeft = gridManager.GridToWorld(Vector2Int.zero);
            Vector3 topRight = gridManager.GridToWorld(new Vector2Int(width - 1, height - 1));

            float minX = Mathf.Min(bottomLeft.x, topRight.x) - boundsPadding;
            float maxX = Mathf.Max(bottomLeft.x, topRight.x) + boundsPadding;
            float minY = Mathf.Min(bottomLeft.y, topRight.y) - boundsPadding;
            float maxY = Mathf.Max(bottomLeft.y, topRight.y) + boundsPadding;

            float halfHeight = Mathf.Max(0.01f, orthographicSize);
            float halfWidth = halfHeight * targetCamera.aspect;

            if (maxX - minX > halfWidth * 2f)
            {
                minX += halfWidth;
                maxX -= halfWidth;
                desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
            }
            else
            {
                desiredPosition.x = (minX + maxX) * 0.5f;
            }

            if (maxY - minY > halfHeight * 2f)
            {
                minY += halfHeight;
                maxY -= halfHeight;
                desiredPosition.y = Mathf.Clamp(desiredPosition.y, minY, maxY);
            }
            else
            {
                desiredPosition.y = (minY + maxY) * 0.5f;
            }

            return desiredPosition;
        }
    }
}
