using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class importScript : MonoBehaviour
{
	public GameObject arCamera;
	public GameObject ARorigin;
	// Start is called before the first frame update
	public GameObject imageForInstance;

	public void PickImage(int maxSize)
	{
		GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);

		//GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
		quad.transform.position = arCamera.transform.position + arCamera.transform.forward * 2.5f;
		quad.transform.forward = arCamera.transform.forward;

		NativeGallery.Permission permission = NativeGallery.GetImageFromGallery((path) =>
		{
			Debug.Log("Image path: " + path);
			if (path != null)
			{
				// Create Texture from selected image
				Texture2D texture = NativeGallery.LoadImageAtPath(path, maxSize);
				if (texture == null)
				{
					Debug.Log("Couldn't load texture from " + path);
					return;
				}
				quad.transform.localScale = new Vector3(1f, texture.height / (float)texture.width, 1f);
				// Assign texture to a temporary quad and destroy it after 5 seconds


				Material material = quad.GetComponent<Renderer>().material;
				//if (!material.shader.isSupported) // happens when Standard shader is not included in the build
				//material.shader = Shader.Find("Legacy Shaders/Diffuse");

				material.mainTexture = texture;

				//Destroy(quad, 5f);

				// If a procedural texture is not destroyed manually, 
				// it will only be freed after a scene change
				//Destroy(texture, 5f);
			}
		}, "Select a PNG image", "image/png");

		Debug.Log("Permission result: " + permission);
	}


	public void instancePrefeb()
    {
		Instantiate(imageForInstance, new Vector3(0, 0, 0), Quaternion.identity);
	}

}

	 