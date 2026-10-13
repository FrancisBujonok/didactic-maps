// storage.js - guardado en IndexedDB (la base de datos del navegador).
// Se usa IndexedDB y no localStorage porque la imagen del mapa puede pesar varios MB.
// Funciona como un diccionario: get(clave), set(clave, valor), remove(clave).

const DB_NAME = 'mapa-mundos';
const STORE = 'kv';

function openDb() {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, 1);
    request.onupgradeneeded = () => request.result.createObjectStore(STORE);
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

// Abre la base, ejecuta una operación dentro de una transacción y la cierra.
async function run(mode, action) {
  const db = await openDb();
  try {
    return await new Promise((resolve, reject) => {
      const tx = db.transaction(STORE, mode);
      const request = action(tx.objectStore(STORE));
      tx.oncomplete = () => resolve(request.result);
      tx.onerror = () => reject(tx.error);
      tx.onabort = () => reject(tx.error);
    });
  } finally {
    db.close();
  }
}

export const get = (key) => run('readonly', (store) => store.get(key)).then((value) => value ?? null);
export const set = (key, value) => run('readwrite', (store) => store.put(value, key)).then(() => null);
export const remove = (key) => run('readwrite', (store) => store.delete(key)).then(() => null);
