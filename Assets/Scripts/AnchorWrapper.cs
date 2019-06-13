using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.WSA;
using Microsoft.Azure.SpatialAnchors;

public class AnchorWrapper : MonoBehaviour
{
    // Singleton Instance
    private static AnchorWrapper _instance;
    public static AnchorWrapper Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<AnchorWrapper>();
            }
            return _instance;
        }
    }

    // Azure Spatial Anchors resource: Spatial Anchors account ID & Key
    public string SpatialAnchorsAccountId { get; private set; } = "cff005f1-1d07-426f-a593-96ccbe1f7885";
    public string SpatialAnchorsAccountKey { get; private set; } = "E31K2VrqVs886n6/SdazS9SMFek056VReJ+HpBaB4zk=";
    public string AppSharingUrl { get; set; } = "";

    // Session Events
    public event AnchorLocatedDelegate OnAnchorLocated;
    public event LocateAnchorsCompletedDelegate OnLocateAnchorsCompleted;
    public event SessionErrorDelegate OnSessionError;
    public event SessionUpdatedDelegate OnSessionUpdated;
    public event OnLogDebugDelegate OnLogDebug;


    private readonly List<string> AnchorIdsToLocate = new List<string>();
    private readonly Queue<Action> dispatchQueue = new Queue<Action>();

    // AnchorSession
    private CloudSpatialAnchorSession cloudSpatialAnchorSession = null;
    private AnchorLocateCriteria anchorLocateCriteria = null;

    // Session Status
    // Type
    public enum SessionStatusIndicatorType
    {
        RecommendedForCreate = 0,
        ReadyForCreate
    }
    // Actual Session Status Indicators
    private readonly float[] SessionStatusIndicators = new float[2];
    public float GetSessionStatusIndicator(SessionStatusIndicatorType indicatorType)
    {
        return SessionStatusIndicators[(int)indicatorType];
    }

    private bool enableSession;
    public bool EnableSession
    {
        get
        {
            return enableSession;
        }
        set
        {
            if (enableSession != value)
            {
                enableSession = value;
                Debug.Log($"Processing {enableSession}");
                if (enableSession)
                {
                    cloudSpatialAnchorSession.Start();
                }
                else
                {
                    cloudSpatialAnchorSession.Stop();
                }
            }
        }
    }


    // Main 
    private void Awake()
    {
    }

    private void Start()
    {
        anchorLocateCriteria = new AnchorLocateCriteria();
        InitializeCloudSession();
    }

    private void Update()
    {
        lock (dispatchQueue)
        {
            while (dispatchQueue.Count > 0)
            {
                dispatchQueue.Dequeue()();
            }
        }
    }

    private void OnDestroy()
    {
        enableSession = false;

        if (cloudSpatialAnchorSession != null)
        {
            cloudSpatialAnchorSession.Stop();
            cloudSpatialAnchorSession.Dispose();
            cloudSpatialAnchorSession = null;
        }

        if (anchorLocateCriteria != null)
        {
            anchorLocateCriteria = null;
        }

        _instance = null;
    }

    // Public Methods: Session
    public void ResetCloudSession(Action completionRoutine = null)
    {
        Debug.Log("ASA Info: Resetting the session.");
        bool sessionWasEnabled = EnableSession;  // Record the status before the reset
        EnableSession = false;  // cloudSpatialAnchorSession.Stop()
        cloudSpatialAnchorSession.Reset();  // Reset session
        ResetSessionStatusIndicators();  // Reset indicators
        Task.Run(async () =>
        {
            while (LocateOperationInFlight())
            {
                await Task.Yield();  //????
            }
            lock (dispatchQueue)
            {
                dispatchQueue.Enqueue(() =>
                {
                    EnableSession = sessionWasEnabled;
                    completionRoutine?.Invoke();
                });
            }
        });
    }
    
    public void ResetSessionStatusIndicators()
    {
        for (int i = 0; i < SessionStatusIndicators.Length; i++)
        {
            SessionStatusIndicators[i] = 0;
        }
    }

    public CloudSpatialAnchorWatcher CreateWatcher()
    {
        if (SessionValid())
        {
            return cloudSpatialAnchorSession.CreateWatcher(anchorLocateCriteria);
        }
        else
        {
            return null;
        }
    }

    // Public Methods: AnchorLocateCriteria
    public void SetBypassCache(bool BypassCache)
    {
        anchorLocateCriteria.BypassCache = BypassCache;
    }

    public void SetLocateStrategy(bool UseGraph, bool OnlyGraph = false)   
    {
        anchorLocateCriteria.Strategy = UseGraph ?
            (OnlyGraph ? LocateStrategy.Relationship : LocateStrategy.AnyStrategy) : //T,T; T,F
            LocateStrategy.VisualInformation;//F,F
    }

    public void SetNearbyAnchor(CloudSpatialAnchor SourceAnchor, float DistanceInMeters=5.0f, int MaxResultCount=20)
    {
        if (SourceAnchor == null)
        {
            anchorLocateCriteria.NearAnchor = new NearAnchorCriteria();
            return;
        }

        NearAnchorCriteria nearAnchorCriteria = new NearAnchorCriteria();
        nearAnchorCriteria.SourceAnchor = SourceAnchor;
        nearAnchorCriteria.DistanceInMeters = DistanceInMeters;
        nearAnchorCriteria.MaxResultCount = MaxResultCount;
        anchorLocateCriteria.NearAnchor = nearAnchorCriteria;
    }

    // Public Methods: AnchorIdList 

    public void SetAnchorIdsToLocate(IEnumerable<string> anchorIds)
    {
        if (anchorIds == null)
        {
            throw new ArgumentNullException(nameof(anchorIds));
        }
        AnchorIdsToLocate.Clear();
        AnchorIdsToLocate.AddRange(anchorIds);
        anchorLocateCriteria.Identifiers = AnchorIdsToLocate.ToArray();
    }

    public void ResetAnchorIdsToLocate()
    {
        AnchorIdsToLocate.Clear();
        anchorLocateCriteria.Identifiers = new string[0];
    }

    // Public Session Status 
    public bool LocateOperationInFlight()
    {
        return (SessionValid() && cloudSpatialAnchorSession.GetActiveWatchers().Count > 0);
    }

    public bool SessionValid()
    {
        return cloudSpatialAnchorSession != null;
    }


    // Public Task: Cloud Operation

    public async Task<CloudSpatialAnchor> StoreAnchorInCloud(CloudSpatialAnchor cloudSpatialAnchor)
    {
        if (SessionStatusIndicators[(int)SessionStatusIndicatorType.ReadyForCreate] < 1)
        {
            return null;
        }

        await cloudSpatialAnchorSession.CreateAnchorAsync(cloudSpatialAnchor);

        return cloudSpatialAnchor;  // why return?
    }

    public async Task DeleteAnchorAsync(CloudSpatialAnchor cloudSpatialAnchor)
    {
        if (SessionValid())
        {
            await cloudSpatialAnchorSession.DeleteAnchorAsync(cloudSpatialAnchor);
        }
    }

    // Initial Session
    private void InitializeCloudSession()
    {
        cloudSpatialAnchorSession = new CloudSpatialAnchorSession();

        cloudSpatialAnchorSession.Configuration.AccountId = SpatialAnchorsAccountId.Trim();
        cloudSpatialAnchorSession.Configuration.AccountKey = SpatialAnchorsAccountKey.Trim();

        cloudSpatialAnchorSession.LogLevel = SessionLogLevel.All;

        cloudSpatialAnchorSession.Error += CloudSpatialAnchorSession_Error;
        cloudSpatialAnchorSession.OnLogDebug += CloudSpatialAnchorSession_OnLogDebug;
        cloudSpatialAnchorSession.SessionUpdated += CloudSpatialAnchorSession_SessionUpdated;
        cloudSpatialAnchorSession.AnchorLocated += CloudSpatialAnchorSession_AnchorLocated;
        cloudSpatialAnchorSession.LocateAnchorsCompleted += CloudSpatialAnchorSession_LocateAnchorsCompleted;

        cloudSpatialAnchorSession.Start();
        Debug.Log("ASA Info: Session was initialized.");
    }

    private void CloudSpatialAnchorSession_SessionUpdated(object sender, SessionUpdatedEventArgs args)
    {
        SessionStatusIndicators[(int)SessionStatusIndicatorType.ReadyForCreate] = args.Status.ReadyForCreateProgress;
        SessionStatusIndicators[(int)SessionStatusIndicatorType.RecommendedForCreate] = args.Status.RecommendedForCreateProgress;
        OnSessionUpdated?.Invoke(sender, args);
    }

    private void CloudSpatialAnchorSession_LocateAnchorsCompleted(object sender, LocateAnchorsCompletedEventArgs args)
    {
        OnLocateAnchorsCompleted?.Invoke(sender, args);
    }

    private void CloudSpatialAnchorSession_AnchorLocated(object sender, AnchorLocatedEventArgs args)
    {
        OnAnchorLocated?.Invoke(sender, args);
    }

    private void CloudSpatialAnchorSession_OnLogDebug(object sender, OnLogDebugEventArgs args)
    {
        OnLogDebug?.Invoke(sender, args);
    }

    private void CloudSpatialAnchorSession_Error(object sender, SessionErrorEventArgs args)
    {
        OnSessionError?.Invoke(sender, args);
    }
}
