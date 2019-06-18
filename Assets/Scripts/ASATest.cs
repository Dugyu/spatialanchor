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
    private Dictionary<string,GameObject> localAnchorGameObjects = new Dictionary<string, GameObject>();

    // Input Template
    public GameObject protoObject;
    public GameObject anchorProto;
    
    // State
    private bool isHolding = false;
    private GameObject currentMovingObject;
    private Vector3 currentMovingVelocity = Vector3.zero;
    private Vector3 currentCumulativeDelta;
    private Vector3 lastCumulativeDelta;
    private int watchedAnchorKeys = -1;

    // Tasks
    private readonly Queue<Action> dispatchQueue = new Queue<Action>();

    // color
    private Color anchorColorYellow = new Color(1.0f, 1.0f, 0.2f);
    private Color anchorColorPink = new Color(1.0f, 0.392f, 0.392f);
    private Color anchorColorPurple = new Color(0.392f, 0.392f, 0.8f);

    private void Awake()
    {
        InitializeCloudManager();
        InitializeAnchorExchanger();
    }

    void Start()
    {
         QueueOnUpdate(async () =>
        {
            Debug.Log("StartScene");
            Debug.Log("Starting Fetch Keys");
            await anchorExchanger.FetchExistingKeys(CloudManager.AppSharingUrl);
        });
        recognizer = new GestureRecognizer();
        recognizer.StartCapturingGestures();
        recognizer.SetRecognizableGestures(GestureSettings.ManipulationTranslate | GestureSettings.Tap);
        recognizer.Tapped += HandleTap;
        recognizer.ManipulationUpdated += OnManupulationUpdated;
        recognizer.ManipulationStarted += OnManipulationStarted;
        recognizer.ManipulationCompleted += OnManipulationCompleted;
        recognizer.ManipulationCanceled += OnManipulationCanceled;

    }

    private void Update()
    {
        if (isHolding == true && currentMovingObject != null)
        {
            currentMovingVelocity = currentCumulativeDelta - lastCumulativeDelta;
            currentMovingObject.transform.position = currentMovingObject.transform.position + currentMovingVelocity;
        }

        
        int currentKeyCount = anchorExchanger.AnchorKeyCount;
        if (watchedAnchorKeys > 0 && currentKeyCount > watchedAnchorKeys)
        {
            int differ = currentKeyCount - watchedAnchorKeys;
            List<string> newKeys = new List<string>();
            for (int i = 0; i<differ; i++)
            {
                localAnchorIds.Add(anchorExchanger.AnchorKeys[watchedAnchorKeys + i]);
                newKeys.Add(localAnchorIds[localAnchorIds.Count - 1]);
            }
            watchedAnchorKeys = currentKeyCount;
            // add extra watcher here
            AnchorExchanger_IncreaseWatcher(newKeys);
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
            Debug.Log("mani: " + anchorExchanger.AnchorKeys.Count.ToString());

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

    private void InitializeCloudManager()
    {
        CloudManager = AnchorWrapper.Instance;
        CloudManager.OnAnchorLocated += CloudManager_OnAnchorLocated;
        CloudManager.OnLocateAnchorsCompleted += CloudManager_OnLocateAnchorsCompleted;
        CloudManager.OnLogDebug += CloudManager_OnLogDebug;
        CloudManager.OnSessionError += CloudManager_OnSessionError;
        CloudManager.OnSessionUpdated += CloudManager_SessionUpdated;
    }

    private void InitializeAnchorExchanger()
    {
        anchorExchanger = new AnchorExchanger();
        AnchorExchanger.OnFetchCompleted += AnchorExchanger_OnFetchCompleted;
       
    }

    private void AnchorExchanger_OnFetchCompleted()
    {

        Debug.Log("Delegate Fetch Completed, find existing keys: " + anchorExchanger.AnchorKeys.Count.ToString());
        Debug.Log("Starting WatchKeys.");

        CloudManager.SetAnchorIdsToLocate(anchorExchanger.AnchorKeys);
        CloudManager.CreateWatcher();
        // state change  global broadcast
        localAnchorIds.AddRange(anchorExchanger.AnchorKeys);
        watchedAnchorKeys = localAnchorIds.Count;
     
        anchorExchanger.WatchKeys(CloudManager.AppSharingUrl);
    }

    private void AnchorExchanger_IncreaseWatcher(List<string> localAnchorKeysToAdd)
    {
        Debug.Log("Adding New Watcher");
        CloudManager.SetAnchorIdsToLocate(localAnchorKeysToAdd);
        CloudManager.CreateWatcher(); 
    }

    public void HandleTap(TappedEventArgs tapEvent)
    {
        // Construct a Ray using forward direction of the HoloLens.
        Ray HeadRay = new Ray(tapEvent.headPose.position, tapEvent.headPose.forward);
        Vector3 PointInAir = HeadRay.GetPoint(1.5f);

        // Spawn Object
        GameObject protoCopy = Instantiate(protoObject);
        protoCopy.transform.position = PointInAir;
        protoCopy.transform.rotation = Quaternion.Euler(0, 0, 70);
        // Empty Object to Hold Anchor
        GameObject anchorCopy = Instantiate(anchorProto);
        anchorCopy.transform.position = PointInAir;

        // Bind anchor holder and the spawned object
        protoCopy.transform.SetParent(anchorCopy.transform);
        anchorCopy.AddComponent<WorldAnchor>();

        CloudSpatialAnchor localCloudAnchor = new CloudSpatialAnchor();
        localCloudAnchor.LocalAnchor = anchorCopy.GetComponent<WorldAnchor>().GetNativeSpatialAnchorPtr();

        int index = 0;

        string name = protoCopy.name;
        string scaleX = protoCopy.transform.localScale.x.ToString();
        string scaleY = protoCopy.transform.localScale.y.ToString();
        string scaleZ = protoCopy.transform.localScale.z.ToString();
        string posX = protoCopy.transform.localPosition.x.ToString();
        string posY = protoCopy.transform.localPosition.y.ToString();
        string posZ = protoCopy.transform.localPosition.z.ToString();
        string rotX = protoCopy.transform.localRotation.x.ToString();
        string rotY = protoCopy.transform.localRotation.y.ToString();
        string rotZ = protoCopy.transform.localRotation.z.ToString();
        string rotW = protoCopy.transform.localRotation.w.ToString();

        localCloudAnchor.AppProperties.Add(index.ToString() + "-name", name);
        localCloudAnchor.AppProperties.Add(index.ToString() + "-scaleX", scaleX);
        localCloudAnchor.AppProperties.Add(index.ToString() + "-scaleY", scaleY);
        localCloudAnchor.AppProperties.Add(index.ToString() + "-scaleZ", scaleZ);
        localCloudAnchor.AppProperties.Add(index.ToString() + "-posX", posX);
        localCloudAnchor.AppProperties.Add(index.ToString() + "-posY", posY);
        localCloudAnchor.AppProperties.Add(index.ToString() + "-posZ", posZ);
        localCloudAnchor.AppProperties.Add(index.ToString() + "-rotX", rotX);
        localCloudAnchor.AppProperties.Add(index.ToString() + "-rotY", rotY);
        localCloudAnchor.AppProperties.Add(index.ToString() + "-rotZ", rotZ);
        localCloudAnchor.AppProperties.Add(index.ToString() + "-rotW", rotW);


        localCloudAnchor.Expiration = DateTimeOffset.Now.AddDays(2);
        Debug.Log(CloudManager.EnoughDataToCreate);

        currentCloudAnchor = null;

        Task.Run(async () =>
        {
            QueueOnUpdate(() =>
            {
                Debug.Log("In Task.......");
                anchorCopy.GetComponent<MeshRenderer>().material.color = anchorColorYellow;
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

                        //  Store in Local GameObject Dictionary
                        localAnchorGameObjects.Add(currentCloudAnchor.Identifier, anchorCopy);

                        Debug.Log(currentCloudAnchor.Identifier);
                        Debug.Log("anchorNumber: " + anchorNumber.ToString());
                        anchorCopy.GetComponent<MeshRenderer>().material.color = anchorColorPink;
                    });
                }
                else
                {
                    QueueOnUpdate(() =>
                   {
                       Debug.Log("SaveFailed!");
                       anchorCopy.GetComponent<MeshRenderer>().material.color = anchorColorPurple;

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
        switch (args.Status)
        {
            case LocateAnchorStatus.Located:

                // if this anchor has already been added to local scene objects
                if (localAnchorGameObjects.ContainsKey(args.Anchor.Identifier))
                {
                    QueueOnUpdate(() =>
                    {
                        GameObject parent = localAnchorGameObjects[args.Anchor.Identifier];

                        int childCount = parent.transform.childCount;

                        for (int i = 0; i < childCount; i++)
                        {
                            Transform child = parent.transform.GetChild(i);
                            MoveObjectUsingAppProperties(child.transform, args, i);
                        }
                    });
                }
                // if this anchor is yet to be added to the local scene objects
                else
                {
                    QueueOnUpdate(() => 
                    {
                        Debug.Log("located: " + args.Anchor.Identifier);
                        Debug.Log("versionTag: " + args.Anchor.VersionTag);
                        GameObject anchorCube = Instantiate(anchorProto);
                        anchorCube.GetComponent<MeshRenderer>().material.color = anchorColorPink;
                        anchorCube.AddComponent<WorldAnchor>();
                        anchorCube.GetComponent<WorldAnchor>().SetNativeSpatialAnchorPtr(args.Anchor.LocalAnchor);
                        int id = 0;
                        if (args.Anchor.AppProperties.Count > 0)
                            {
                                GameObject child = Instantiate(protoObject, anchorCube.transform);
                                MoveObjectUsingAppProperties(child.transform, args, id);
                            }
                    });
                }

                break;
            case LocateAnchorStatus.AlreadyTracked:
                Debug.Log("Already tracked! " + args.Anchor.Identifier);
                break;
            case LocateAnchorStatus.NotLocated:
                Debug.Log("notLocated");
                break;
            case LocateAnchorStatus.NotLocatedAnchorDoesNotExist:
                Debug.Log("NotExist");
                break;
        }
    }

    private void MoveObjectUsingAppProperties(Transform obj, AnchorLocatedEventArgs args, int id)
    {
        Vector3 pos = Vector3.zero;
        pos.x = float.Parse(args.Anchor.AppProperties[id.ToString() + "-posX"]);
        pos.y = float.Parse(args.Anchor.AppProperties[id.ToString() + "-posY"]);
        pos.z = float.Parse(args.Anchor.AppProperties[id.ToString() + "-posZ"]);

        Vector3 scale = Vector3.zero;
        scale.x = float.Parse(args.Anchor.AppProperties[id.ToString() + "-scaleX"]);
        scale.y = float.Parse(args.Anchor.AppProperties[id.ToString() + "-scaleY"]);
        scale.z = float.Parse(args.Anchor.AppProperties[id.ToString() + "-scaleZ"]);

        Quaternion rot = Quaternion.identity;
        rot.x = float.Parse(args.Anchor.AppProperties[id.ToString() + "-rotX"]);
        rot.y = float.Parse(args.Anchor.AppProperties[id.ToString() + "-rotY"]);
        rot.z = float.Parse(args.Anchor.AppProperties[id.ToString() + "-rotZ"]);
        rot.w = float.Parse(args.Anchor.AppProperties[id.ToString() + "-rotW"]);

        obj.transform.localPosition = pos;
        obj.transform.localScale = scale;
        obj.transform.localRotation = rot;
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

    protected void QueueOnUpdate(Action updateAction)
    {
        lock (dispatchQueue)
        {
            dispatchQueue.Enqueue(updateAction);
        }
    }



}
