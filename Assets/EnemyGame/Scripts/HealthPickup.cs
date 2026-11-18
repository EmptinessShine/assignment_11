using UnityEngine;

// medkit on the level. how much it heals is in the ItemData asset.
public class HealthPickup : MonoBehaviour
{
    [SerializeField] ItemData item;

    // Stay, not Enter: if you stand on it with full health and then get hit, it still works
    void OnTriggerStay2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() == null) return;

        Health health = PlayerController.instance.health;
        if (health.currentHealth <= 0 || health.currentHealth >= health.maxHealth) return;

        health.Heal(item.healAmount);
        Destroy(gameObject);
    }
}
