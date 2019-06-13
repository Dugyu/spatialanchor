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

    private AnchorWrapper CloudManager;
    public AnchorExchanger anchorExchanger = new AnchorExchanger();

    // Hand Interaction
    private GestureRecognizer recognizer;
    protected bool tapExecuted = false;

    void Start()
    {
        recognizer = new GestureRecognizer();
        recognizer.StartCapturingGestures();
        recognizer.SetRecognizableGestures(GestureSettings.Tap);
        recognizer.Tapped += HandleTap;
        InitializeSession();
    }

    
    public void ResetSession()
    {

    }

    private void InitializeSession()
    {
        CloudManager = AnchorWrapper.Instance;
        CloudManager.OnAnchorLocated += CloudManager_OnAnchorLocated;
        CloudManager.OnLocateAnchorsCompleted += CloudManager_OnLocateAnchorsCompleted;
        CloudManager.OnLogDebug += CloudManager_OnLogDebug;
        CloudManager.OnSessionError += CloudManager_OnSessionError;
        CloudManager.OnSessionUpdated += CloudManager_SessionUpdated;
    }
    // Used in Resession, Clean up scence objects
    public void CleanupObjects()
    {
    }

    public void HandleTap(TappedEventArgs tapEvent)
        {
            if (tapExecuted)
            {
                return;
            }
            tapExecuted = true;


            // Construct a Ray using forward direction of the HoloLens.
            Ray HeadRay = new Ray(tapEvent.headPose.position, tapEvent.headPose.forward);
            Vector3 PointInAir = HeadRay.GetPoint(1.5f);
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
