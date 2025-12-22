using UnityEngine;

namespace SwampPreachers
{
	public class ParticlePoolObject : PoolObject
	{
		ParticleSystem _particle;

		public override void OnObjectReuse()
		{
			if(!_particle)
				_particle = GetComponent<ParticleSystem>();
			_particle.Play();
		}
	}
}
