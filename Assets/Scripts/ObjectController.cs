using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR.InteractionSystem;

public class ObjectController : MonoBehaviour {

  HashSet<GameObject> currentModels = new HashSet<GameObject>();
  bool full = false;
  GameObject currentModel;

  //Some Object with a collider that represents the target area
  public GameObject constraintArea;
  public bool restrictY = true;

  // Bounds constraintBounds;
  Collider constraintCollider;
  int hands = 0;


  class ConstrainedObject {
    public GameObject obj;
    public Transform parent1;
    public Transform parent2;
    public Vector3 offset1;
    public Vector3 offset2;
    public float yPos;
    public Quaternion rot;
    public Quaternion areaRot;
    public Transform oldParent;
  }

  ConstrainedObject constrainedObject;

  // List<ConstrainedObject> constrainedObjects = new List<ConstrainedObject>();

	// Use this for initialization
	void Start () {
		constraintCollider = constraintArea.GetComponent<Collider>();
	}

	// Update is called once per frame
	void Update () {
    if (constrainedObject != null) {
      ConstrainObject(constrainedObject);
    }
	}

  public bool InConstrainedArea(GameObject obj) {
    if ((constrainedObject != null && constrainedObject.obj == obj) || currentModels.Contains(obj)) {
      return true;
    }
    return false;
  }

  public void GrabObject(GameObject g, Hand hand) {


  }

  public void ReleaseObject(GameObject g, Hand hand) {


  }

  public void AddConstrainedObject(GameObject obj, Transform parent, Vector3 offset) {
    hands++;
    if (constrainedObject != null) {
      constrainedObject.parent2 = parent;
      constrainedObject.offset2 = offset;
      return;
    }
    ConstrainedObject co = new ConstrainedObject();
    co.obj = obj;
    co.parent1 = parent;
    co.offset1 = offset;
    co.yPos = obj.transform.position.y;
    co.rot = obj.transform.rotation;
    co.areaRot = constraintArea.transform.rotation;
    co.oldParent = obj.transform.parent;
    constrainedObject = co;


    obj.transform.parent = parent;
    Rigidbody rb = obj.GetComponent<Rigidbody>();
    if (rb) {
      rb.isKinematic = true;
    }
  }

  public void RemoveConstrainedObject(Transform parent) {
    hands--;
    if (hands > 0) {
      if (constrainedObject.parent1 == parent) {
        constrainedObject.parent1 = constrainedObject.parent2;
        constrainedObject.offset1 = constrainedObject.offset2;
      }
      constrainedObject.parent2 = null;
      constrainedObject.rot = constrainedObject.obj.transform.rotation;
      constrainedObject.yPos = constrainedObject.obj.transform.position.y;
      return;
    }
    constrainedObject.obj.transform.parent = constrainedObject.oldParent;
    Rigidbody rb = constrainedObject.obj.GetComponent<Rigidbody>();
    if (rb) {
      rb.isKinematic = true;
    }
    constrainedObject = null;
  }

  void ConstrainObject(ConstrainedObject co) {
    //Desired location = parent + offset
    //If within Bounds, all is well
    //Otherwise, move object to closest point in constrained area
    Vector3 desiredLocation = Vector3.zero;
    if (hands == 1) {
      desiredLocation = co.parent1.position + co.offset1;
      co.obj.transform.rotation = co.rot;
    } else if (hands == 2) {
      Vector3 desiredLoc1 = co.parent1.position + co.offset1;
      Vector3 desiredLoc2 = co.parent2.position + co.offset2;
      desiredLocation = desiredLoc1 + (desiredLoc2 - desiredLoc1) * .5f;
    }

    Vector3 boundPoint = constraintCollider.ClosestPoint(desiredLocation);
    if (boundPoint != desiredLocation) {
      //Outside of bounds
      co.obj.transform.position = boundPoint;
    }


    if (restrictY && hands == 1) {
      co.obj.transform.position = new Vector3(co.obj.transform.position.x, co.yPos, co.obj.transform.position.z);
    }
  }

  //Perform fullness check when removing an object from UI
  public void RemoveObject(GameObject g) {
    if (currentModels.Remove(g)) {
      g.transform.parent = null;
    }
  }

  public bool AddObject(GameObject g) {
    if (currentModels.Add(g)) {
      g.transform.parent = constraintArea.transform;
    }
    return currentModels.Add(g);
  }

  public bool IsFull() {
    return constrainedObject != null || currentModels.Count != 0;
  }

  // void OnTriggerEnter(Collider other) {
    // print("Trigger Enter: " + other.gameObject.name);
    // currentModels.Add(other.gameObject);
  // }

  // void OnTriggerExit(Collider other) {
    // print("Trigger Exit: " + other.gameObject.name);
    // currentModels.Remove(other.gameObject);
  // }
}
