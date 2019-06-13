using Microsoft.Azure.SpatialAnchors;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.WSA;
using UnityEngine.XR.WSA.Input;

public class ASATest : MonoBehaviour
{
    // Session Manager Class
    private AnchorWrapper CloudManager;
    public AnchorExchanger anchorExchanger = new AnchorExchanger();

    // Hand Interaction
    private GestureRecognizer recognizer;
    protected bool tapExecuted = false;

    // Spactial Anchor, Watcher
    public CloudSpatialAnchor currentCloudAnchor;
    public CloudSpatialAnchorWatcher currentWatcher;


    // Input Template
    public GameObject protoObject;

    private bool isHolding;
    private GameObject currentMovingObject;

    
    private Vector3 currentMovingVelocity = Vector3.zero;
    private Vector3 currentCumulativeDelta;
    private Vector3 lastCumulativeDelta;

    void Start()
    {
        recognizer = new GestureRecognizer();
        recognizer.StartCapturingGestures();
        recognizer.SetRecognizableGestures(GestureSettings.ManipulationTranslate | GestureSettings.Tap);
        recognizer.Tapped += HandleTap;
        recognizer.ManipulationUpdated += OnManupulationUpdated;
        recognizer.ManipulationStarted += OnManipulationStarted;
        recognizer.ManipulationCompleted += OnManipulationCompleted;
        recognizer.ManipulationCanceled += OnManipulationCanceled;
        InitializeCloudManager();
    }


    private void Update()
    {
        if (isHolding == true && currentMovingObject != null)
        {
            currentMovingVelocity = currentCumulativeDelta - lastCumulativeDelta;
            currentMovingObject.transform.position = currentMovingObject.transform.position + currentMovingVelocity;
        }
    }


    private void OnManupulationUpdated(ManipulationUpdatedEventArgs obj)
    {
        lastCumulativeDelta = currentCumulativeDelta;

        Debug.Log("M-Update");
        currentCumulativeDelta = obj.cumulativeDelta;

    }
    private void OnManipulationStarted(ManipulationStartedEventArgs obj)
    {
        Debug.Log("M-Startingggggggggggggg");
        currentCumulativeDelta = Vector3.zero;

        Ray HeadRay = new Ray(obj.headPose.position, obj.headPose.forward);
        RaycastHit hit;
        if (Physics.Raycast(HeadRay, out hit) )
        {
            if ( hit.transform.tag == "Proto")
            {
                Debug.Log("Hit!");
                currentMovingObject = hit.transform.gameObject;
                isHolding = true;
            }
        }
    }


    private void OnManipulationCompleted(ManipulationCompletedEventArgs obj)
    {
        Debug.Log("M-Ending");
        isHolding = false;
        currentMovingObject = null;
        
    }

    private void OnManipulationCanceled(ManipulationCanceledEventArgs obj)
    {  
        Debug.Log("M-Cancel");
        isHolding = false;
        currentMovingObject = null;
    }



    private void InitializeCloudManager()
    {
        CloudManager = AnchorWrapper.Instance;
        CloudManager.OnAnchorLocated += CloudManager_OnAnchorLocated;
        CloudManager.OnLocateAnchorsCompleted += CloudManager_OnLocateAnchorsCompleted;
        CloudManager.OnLogDebug += CloudManager_OnLogDebug;
        CloudManager.OnSessionError += CloudManager_OnSessionError;
        CloudManager.OnSessionUpdated += CloudManager_SessionUpdated;
    }

    public void HandleTap(TappedEventArgs tapEvent)
        {
            // Construct a Ray using forward direction of the HoloLens.
            Ray HeadRay = new Ray(tapEvent.headPose.position, tapEvent.headPose.forward);
            Vector3 PointInAir = HeadRay.GetPoint(1.5f);
            GameObject protoCopy = Instantiate(protoObject);
            protoCopy.transform.position = PointInAir;
        }

    private void CloudManager_SessionUpdated(object sender, SessionUpdatedEventArgs args)
    {
    }

    private void CloudManager_OnAnchorLocated(object sender, AnchorLocatedEventArgs args)
    {
    }

    private void CloudManager_OnLocateAnchorsCompleted(object sender, LocateAnchorsCompletedEventArgs args)
    {
    }

    private void CloudManager_OnSessionError(object sender, SessionErrorEventArgs args)
    {
        Debug.Log(args.ErrorMessage);
    }

    private void CloudManager_OnLogDebug(object sender, OnLogDebugEventArgs args)
    {
        Debug.Log(args.Message);
    }




}
