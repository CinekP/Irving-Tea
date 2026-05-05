using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class TreeLife : MonoBehaviour
{
    [Header("Settings")]
    public float timeToHeal = 4f;
    private float timer = 0f;
    private bool isBeingHeld = false;
    private bool isHealed = false;

    [Header("Visuals")]
    public GameObject deadVisual;
    public GameObject healthyVisual;

    [Header("Particles")]
    public ParticleSystem chargingParticles; // Te lecą w trakcie trzymania
    public ParticleSystem successBurst;      // Jednorazowy wybuch po 5s
    public ParticleSystem healedIdleParticles; // Ciągłe drobinki wokół uleczonego

    void Update()
    {
        if (isBeingHeld && !isHealed)
        {
            timer += Time.deltaTime;

            // Opcjonalnie: zwiększaj intensywność cząsteczek wraz z upływem czasu
          //  var emission = chargingParticles.emission;
           // emission.rateOverTime = (timer / timeToHeal) * 100f;

            if (timer >= timeToHeal)
            {
                Heal();
            }
        }
    }

    // Wywoływane przez Select Entered
    public void OnGrab()
    {
        if (!isHealed)
        {
            isBeingHeld = true;
            timer = 0f; // Reset licznika w skrypcie

            if (chargingParticles != null)
            {
                // Twardy reset: najpierw stop i czyszczenie, potem start od zera
                chargingParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                chargingParticles.Play();
            }
        }
    }

    // Wywoływane przez Select Exited
    public void OnRelease()
    {
        isBeingHeld = false;
        timer = 0f;
        chargingParticles.Stop();
    }

    void Heal()
    {
        isHealed = true;
        isBeingHeld = false;
        
        deadVisual.SetActive(false);
        healthyVisual.SetActive(true);

        if (chargingParticles != null) 
        {
            chargingParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (successBurst != null) successBurst.Play();
        if (healedIdleParticles != null) healedIdleParticles.Play();
        
        Debug.Log("Drzewo uleczone po 5 sekundach!");
    }
}