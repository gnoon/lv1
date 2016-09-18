/*---PULIGIN---*/
(function ($) {
    if (!$.exist) {
        $.extend({
            exist: function () {
                var ele, cbmExist, cbmNotExist;
                if (arguments.length) {
                    for (x in arguments) {
                        switch (typeof arguments[x]) {
                            case 'function':
                                if (typeof cbmExist == "undefined") cbmExist = arguments[x];
                                else cbmNotExist = arguments[x];
                                break;
                            case 'object':
                                if (arguments[x] instanceof jQuery) ele = arguments[x];
                                else {
                                    var obj = arguments[x];
                                    for (y in obj) {
                                        if (typeof obj[y] == 'function') {
                                            if (typeof cbmExist == "undefined") cbmExist = obj[y];
                                            else cbmNotExist = obj[y];
                                        }
                                        if (typeof obj[y] == 'object' && obj[y] instanceof jQuery) ele = obj[y];
                                        if (typeof obj[y] == 'string') ele = $(obj[y]);
                                    }
                                }
                                break;
                            case 'string':
                                ele = $(arguments[x]);
                                break;
                        }
                    }
                }
                if (!ele) return false;
                if (typeof cbmExist == 'function') {	//	has at least one Callback Method
                    var exist = ele.length > 0 ? true : false; //	strict setting of boolean
                    if (exist) {	// Elements do exist
                        return ele.each(function (i) { cbmExist.apply(this, [exist, ele, i]); });
                    }
                    else if (typeof cbmNotExist == 'function') {
                        cbmNotExist.apply(ele, [exist, ele]);
                        return ele;
                    }
                    else {
                        if (ele.length <= 1) return ele.length > 0 ? true : false;
                        else return ele.length;
                    }
                }
                else {	//	has NO callback method, thus return if exist or not based on element existant length
                    if (ele.length <= 1) return ele.length > 0 ? true : false; //	strict return of boolean
                    else return ele.length; //	return actual length for how many of this element exist
                }

                return false; //	only hits if something errored!
            }
        });
        $.fn.extend({
            exist: function () {
                var args = [$(this)];
                if (arguments.length) for (x in arguments) args.push(arguments[x]);
                return $.exist.apply($, args);
            }
        });
    }
})(jQuery);
/*---PULIGIN---*/

/*$("tbody").append(
    $("<tr />").append( $("<td />").append($("<p />").text("#eleID")), $("<td />").append($("<p />").text($("#eleID").exist())) ),
    $("<tr />").append( $("<td />").append($("<p />").text(".class-name")), $("<td />").append($("<p />").text($(".class-name").exist())) ),
    $("<tr />").append( $("<td />").append($("<p />").text("div:last")), $("<td />").append($("<p />").text($("div:last").exist())) ),
    $("<tr />").append( $("<td />").append($("<p />").text("#noBob")), $("<td />").append($("<p />").text($("#noBob").exist())) )
);*/

/*-------------OTHER CONSOLE STUFF TO LOOK AT FOR EXAMPLES--------------*/
/*if ($.exist('#eleID')) { console.log(1) }		//	param as STRING
if ($.exist($('#eleID'))) { console.log(2) }	//	param as jQuery OBJECT
if ($('#eleID').exist()) { console.log(3) }		//	enduced on jQuery OBJECT
$.exist('#eleID', function(exist) {			//	param is STRING && CALLBACK METHOD
	console.log(4, exist)
})
$.exist($('#eleID'), function(exist) {		//	param is jQuery OBJECT && CALLBACK METHOD
	console.log(5, exist)
})
$('#eleID').exist(function(exist) {			//	enduced on jQuery OBJECT with CALLBACK METHOD
	console.log(6, exist)
})
$.exist({						//	param is OBJECT containing 2 key|value pairs: element = STRING, callback = METHOD
	element: '#eleID',
	callback: function(exist) {
		console.log(7, exist)
	}
})
$.exist({						//	param is OBJECT containing 2 key|value pairs: element = jQuery OBJECT, callback = METHOD
	element: $('#eleID'),
	callback: function(exist) {
		console.log(8, exist)
	}
})
$.exist([						//	param is ARRAY containing 2 key|value pairs: jQuery STRING, METHOD
	'#eleID',
	function(exist) {
		console.log(9, exist)
	}
])
console.log('10A', 'Will only callback if true', $.exist([						//	param is ARRAY containing 2 key|value pairs: jQuery OBJECT, METHOD
	$('#eleID'),
	function() {
		console.log('10A - HEY, I WAS TRUE!')
	}
]))
console.log('10B', 'Will only callback if true', $.exist([						//	param is ARRAY containing 2 key|value pairs: jQuery OBJECT, METHOD
	$('#doeNOTexist'),
	function() {
		console.log('10B - HEY, I WAS TRUE!')
	}
]))
console.log(11, $.exist({ element: '#eleID' }))		//	param is OBJECT containing 1 key|value pairs: element = STRING
console.log(12, $.exist({ element: '#doeNOTexist' }))		//	param is OBJECT containing 1 key|value pairs: element = STRING
console.log(13, $.exist({ element: $('#eleID') }))		//	param is OBJECT containing 1 key|value pairs: element = jQuery OBJECT
console.log(14, $.exist({ element: $('#doeNOTexist') }))		//	param is OBJECT containing 1 key|value pairs: element = jQuery OBJECT
*/