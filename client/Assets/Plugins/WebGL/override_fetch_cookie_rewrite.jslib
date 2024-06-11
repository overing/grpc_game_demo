mergeInto(LibraryManager.library, {
    $targetUrlPrefix: 0,
    $originalfetch: 0,

    $OverrideFatch__deps: [ '$targetUrlPrefix', '$originalfetch' ],
    $OverrideFatch: function () {
        fetch = function (url, data) {
            if (url.indexOf(targetUrlPrefix) === 0) {
                data.credentials = 'include';
            }
            return originalfetch(url, data);
        };
        fetch.overridden = true;
    },

    OverrideFatchCookieRewrite__deps: [ '$targetUrlPrefix', '$originalfetch', '$OverrideFatch' ],
    OverrideFatchCookieRewrite: function (prefix) {
        targetUrlPrefix = UTF8ToString(prefix);
        if (fetch.overridden !== true) {
            originalfetch = fetch;
            OverrideFatch();
        }
    }
});