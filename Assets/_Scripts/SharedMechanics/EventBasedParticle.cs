using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventBasedParticle : MonoBehaviour
{
    [SerializeField] private Transform particleParent;
    private IDamageHandler damageHandler;

    private void Start()
    {
        damageHandler = GetComponent<IDamageHandler>();

        if(damageHandler != null)
        {
            damageHandler.OnTakeDamage += HitParticle;
            damageHandler.OnClearingCorpse += PoofOut;
        }
    }

    private void OnDisable()
    {
        if(damageHandler != null)
        {
            damageHandler.OnTakeDamage -= HitParticle;
            damageHandler.OnClearingCorpse -= PoofOut;
        }
    }

    public void HitParticle(int damage, Vector3 contactPoint, WeaponType weaponType)
        => ObjectPooler.poolerInstance.SpawnFromPool("ImpactVFX", contactPoint, Quaternion.identity);


    public void PoofOut(GameObject agent, Vector3 position)
        => ObjectPooler.poolerInstance.SpawnFromPool("PoofOut", position, Quaternion.identity);
        
    
}
