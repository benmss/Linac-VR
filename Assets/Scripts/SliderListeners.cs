using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Valve.VR.InteractionSystem {

  public class SliderListeners : MonoBehaviour {

    public Slider gantryLR;
    public LinacRotation gantry;
    public Slider bedLR;
    Player player = null;
    public GameObject[] sliders;
    Slider[] scripts;

    public EventSystem eventSystem;

    int current = 0;
    int max;
    int upCounter = 0;
    int changeMax = 20;
    int downCounter = 0;

    Slider currentSlider;

    // Use this for initialization
    void Start () {
      player = InteractionSystem.Player.instance;
      //Attach listeners to sliders
      if (gantryLR) {
        gantryLR.onValueChanged.AddListener(delegate {gantry.Rotate(gantryLR.value);});
        // gantry.Rotate(gantryLR.value);
      }

      scripts = new Slider[sliders.Length];
      for (int i = 0; i < sliders.Length; i++) {
        scripts[i] = sliders[i].GetComponent<Slider>();
        // print(sliders[i] + ", " + scripts[i]);
      }
      currentSlider = scripts[0];
      max = sliders.Length-1;
      upCounter = changeMax;
      downCounter = changeMax;
    }

    void Update() {
      if (!player || player.hands == null) { return; }
      foreach ( Hand hand in player.hands ) {
        if (hand.startingHandType == Hand.HandType.Left) {
          if (!hand) { continue; }
          if (hand.controller == null) { continue; }
          Vector2 v = hand.controller.GetAxis();
          if (v.x > 0.5 && v.y < 0.5 && v.y > -0.5) {
            //Right
            currentSlider.value += 1;
          } else if (v.x < -0.5 && v.y < 0.5 && v.y > -0.5) {
            //Left
            currentSlider.value -= 1;
          } else if (v.y > 0.5 && v.x < 0.5 && v.x > -0.5) {
            //Up
            if (upCounter < changeMax) {
              upCounter++;
              continue;
            }

            if (current > 0) {
              current--;
              eventSystem.SetSelectedGameObject(sliders[current]);
              currentSlider = scripts[current];
            }
            upCounter = 0;
          } else if (v.y < -0.5 && v.x < 0.5 && v.x > -0.5) {
            //Down
            if (downCounter < changeMax) {
              downCounter++;
              continue;
            }

            if (current < max) {
              current++;
              eventSystem.SetSelectedGameObject(sliders[current]);
              currentSlider = scripts[current];
            }
            downCounter = 0;
          } else {
            //Neutral
            upCounter = changeMax;
            downCounter = changeMax;
          }
        }
      }
    }
  }
}
