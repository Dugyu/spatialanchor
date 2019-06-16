using Microsoft.Azure.SpatialAnchors;
using System;
using System.Linq;
using System.Collections;
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
    // local anchor
    private List<string> localAnchorIds = new List<string>();


    // Input Template
    public GameObject protoObject;
    public GameObject anchorProto;

    // State
    private bool isHolding;
    private GameObject currentMovingObject;
    private Vector3 currentMovingVelocity = Vector3.zero;
    private Vector3 currentCumulativeDelta;
    private Vector3 lastCumulativeDelta;

    // Tasks
    private readonly Queue<Action> dispatchQueue = new Queue<Action>();

    int count;

    private void Awake()
    {
        InitializeCloudManager();
        anchorExchanger = new AnchorExchanger();
    }


    void Start()
    {

        QueueOnUpdate(() => 
        {
            anchorExchanger.FetchExistingKeys(CloudManager.AppSharingUrl);
        });

        QueueOnUpdate(() =>
        {
            localAnchorIds.AddRange(anchorExchanger.AnchorKeys);
            count = localAnchorIds.Count;
        });

        QueueOnUpdate(() =>
        {
            Debug.Log("StartScene");
            Debug.Log(count);

            for (int i = 0; i < count; i++)
            {
                Debug.Log("No. " + i + ": " + localAnchorIds[i]);
            }
        });


        recognizer = new GestureRecognizer();
        recognizer.StartCapturingGestures();
        recognizer.SetRecognizableGestures(GestureSettings.ManipulationTranslate | GestureSettings.Tap);
        recognizer.Tapped += HandleTap;
        recognizer.ManipulationUpdated += OnManupulationUpdated;
        recognizer.ManipulationStarted += OnManipulationStarted;
        recognizer.ManipulationCompleted += OnManipulationCompleted;
        recognizer.ManipulationCanceled += OnManipulationCanceled;
        anchorExchanger.WatchKeys(CloudManager.AppSharingUrl);

    }


    private void Update()
    {
        if (isHolding == true && currentMovingObject != null)
        {
            currentMovingVelocity = currentCumulativeDelta - lastCumulativeDelta;
            currentMovingObject.transform.position = currentMovingObject.transform.position + currentMovingVelocity;
            
        }

        lock (dispatchQueue)
        {
            if (dispatchQueue.Count > 0)
            {
                dispatchQueue.Dequeue()();
            }
        }

    }


    private void OnManupulationUpdated(ManipulationUpdatedEventArgs obj)
    {
        lastCumulativeDelta = currentCumulativeDelta;
        currentCumulativeDelta = obj.cumulativeDelta;

    }
    private void OnManipulationStarted(ManipulationStartedEventArgs obj)
    {
        Debug.Log("M-Starting");
        currentCumulativeDelta = Vector3.zero;

        Ray HeadRay = new Ray(obj.headPose.position, obj.headPose.forward);
        RaycastHit hit;
        if (Physics.Raycast(HeadRay, out hit) )
        {
            if ( hit.transform.tag == "Proto")
            {
                currentMovingObject = hit.transform.gameObject;
                isHolding = true;
            }
        }

        QueueOnUpdate(() =>
        {
            Debug.Log(anchorExchanger.AnchorKeys.Count);
            for (int i = 0; i < anchorExchanger.AnchorKeys.Count; i++)
            {
                Debug.Log(anchorExchanger.AnchorKeys[i]);
            }
        });
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

    protected void QueueOnUpdate(Action updateAction)
    {
        lock (dispatchQueue)
        {
            dispatchQueue.Enqueue(updateAction);
        }
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

        // Spawn Object
        GameObject protoCopy = Instantiate(protoObject);
        protoCopy.transform.position = PointInAir;

        // Empty Object to Hold Anchor
        GameObject anchorCopy = Instantiate(anchorProto);
        anchorCopy.transform.position = PointInAir;

        // Bind anchor holder and the spawned object
        protoCopy.transform.SetParent(anchorCopy.transform);
        anchorCopy.AddComponent<WorldAnchor>();

        CloudSpatialAnchor localCloudAnchor = new CloudSpatialAnchor();
        localCloudAnchor.LocalAnchor = anchorCopy.GetComponent<WorldAnchor>().GetNativeSpatialAnchorPtr();
       
        localCloudAnchor.Expiration = DateTimeOffset.Now.AddDays(2);
        Debug.Log(CloudManager.EnoughDataToCreate);

        currentCloudAnchor = null;



        Task.Run(async () =>
        {
            QueueOnUpdate(() =>
            {
                Debug.Log("In Task.......");
                anchorCopy.GetComponent<MeshRenderer>().material.color = new Color(1.0f, 1.0f, 0.2f);
            });


            while (!CloudManager.EnoughDataToCreate)
            {
                await Task.Delay(300);
                float createProgress = CloudManager.GetSessionStatusIndicator(AnchorWrapper.SessionStatusIndicatorType.RecommendedForCreate);
                QueueOnUpdate(new Action(() => Debug.Log(createProgress)));
            }

            bool success = false;

            try
            {
                QueueOnUpdate(new Action(() => Debug.Log("Saving...")));
                currentCloudAnchor = await CloudManager.StoreAnchorInCloud(localCloudAnchor);
                
                success = currentCloudAnchor != null;
                long anchorNumber = -1;

                if (success)
                {
                    anchorNumber = (await anchorExchanger.StoreAnchorKey(currentCloudAnchor.Identifier));
                    QueueOnUpdate(() => 
                    {
                        Debug.Log("SaveSuccess!");
                        Debug.Log(currentCloudAnchor.Identifier);
                        Debug.Log("anchorNumber: " + anchorNumber.ToString());
                        anchorCopy.GetComponent<MeshRenderer>().material.color = new Color(1.0f, 0.392f, 0.392f);
                    });
                }
                else
                {
                    QueueOnUpdate(() =>
                   {
                       Debug.Log("SaveFailed!");
                       anchorCopy.GetComponent<MeshRenderer>().material.color = new Color(0.392f, 0.392f, 0.8f);

                   });
                }
            }

            catch(Exception ex)
            {
                QueueOnUpdate(new Action(() => Debug.LogException(ex)));
            }

        });
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
