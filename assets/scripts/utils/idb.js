export function idbSupported() {
  return typeof indexedDB !== 'undefined';
}

export function openObjectStoreDb(name, version, stores) {
  return new Promise((resolve, reject) => {
    if (!idbSupported()) {
      reject(new Error('IndexedDB is unavailable.'));
      return;
    }

    const request = indexedDB.open(name, version);
    request.onupgradeneeded = () => {
      const db = request.result;
      stores.forEach(({ name: storeName, keyPath, indexes = [] }) => {
        const store = db.objectStoreNames.contains(storeName)
          ? request.transaction.objectStore(storeName)
          : db.createObjectStore(storeName, { keyPath });
        indexes.forEach(({ name: indexName, keyPath: indexKeyPath, options }) => {
          if (!store.indexNames.contains(indexName)) {
            store.createIndex(indexName, indexKeyPath, options);
          }
        });
      });
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
    request.onblocked = () => reject(new Error('IndexedDB upgrade was blocked.'));
  });
}

export function idbRequest(request) {
  return new Promise((resolve, reject) => {
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

export function idbTransactionDone(transaction) {
  return new Promise((resolve, reject) => {
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error);
    transaction.onabort = () => reject(transaction.error || new Error('IndexedDB transaction aborted.'));
  });
}
